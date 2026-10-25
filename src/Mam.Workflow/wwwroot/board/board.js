// The pass: a live view of the restaurant workflow for demos.
// It reads the graph from /board/api/graph, starts a run and follows its events over Server-Sent Events.
"use strict";

const $ = (selector) => document.querySelector(selector);

const LABELS = {
  intake: "Intake",
  researcher: "Researcher",
  brief: "Brief",
  "brand-designer": "Brand designer",
  "menu-writer": "Menu writer",
  gather: "Gather",
  shortlist: "Shortlist",
  "pick-name": "You pick",
  presenter: "Presenter",
};

const STATIONS = [
  { id: "researcher", role: "Searches the web for the market, trends and competition.", empty: "Waiting for the order…" },
  { id: "brand-designer", role: "Three names with a logo each, a mood board and an interior.", empty: "Waiting for the research…" },
  { id: "menu-writer", role: "Writes the menu, in parallel with the brand designer.", empty: "Waiting for the research…" },
  { id: "presenter", role: "Pitches the concept with the name you picked.", empty: "Waiting for your pick…" },
];

const state = {
  runId: null,
  source: null,
  seen: 0,
  received: 0,
  base: Infinity,
  endAt: null,
  graph: null,
  nodes: new Map(),
  lanes: new Map(),
  picked: null,
  concept: null,
};

// ---------- Markdown ----------

const escapeHtml = (text) => text.replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);

// Images are served by this app, but agents write absolute URLs. Relative URLs keep working behind any proxy.
const localize = (text) => (text ?? "").replace(/https?:\/\/[^\s)"'\]]+?(\/images\/)/g, "$1");
const isImage = (href) => /\/images\/[^\s]+\.(png|jpe?g|webp|gif)$/i.test(href ?? "");

marked.use({
  gfm: true,
  renderer: {
    html: ({ text }) => escapeHtml(text),
    link({ href, tokens }) {
      const label = this.parser.parseInline(tokens);
      if (isImage(href)) {
        return `<a class="pic" href="${escapeHtml(href)}" target="_blank" rel="noopener"><img src="${escapeHtml(href)}" alt="" loading="lazy"><span>${label}</span></a>`;
      }
      return `<a href="${escapeHtml(href ?? "")}" target="_blank" rel="noopener">${label}</a>`;
    },
    image({ href, text }) {
      return `<a class="pic" href="${escapeHtml(href)}" target="_blank" rel="noopener"><img src="${escapeHtml(href)}" alt="${escapeHtml(text ?? "")}" loading="lazy"><span>${escapeHtml(text ?? "")}</span></a>`;
    },
  },
});

// Models sometimes wrap their whole answer in a ```markdown fence; unwrap it, even while it is still streaming.
const unfence = (text) => {
  const m = /^\s*```(?:markdown|md)?[^\S\n]*\n([\s\S]*?)(?:\n```\s*)?$/i.exec(text);
  return m ? m[1] : text;
};

const renderMarkdown = (text) => marked.parse(unfence(localize(text)));

// ---------- Time ----------

const formatTime = (ms) => {
  const s = Math.max(0, Math.floor(ms / 1000));
  return `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`;
};

const now = () => (state.endAt ?? (Number.isFinite(state.base) ? Date.now() - state.base : 0));

function tick() {
  if (state.runId) {
    $("#clock").textContent = formatTime(now());
    for (const [id, node] of state.nodes) {
      if (node.timer && node.startAt != null) {
        const end = node.status === "running" ? now() : node.endAt ?? node.startAt;
        node.timer.textContent = formatTime(end - node.startAt);
      }
      const lane = state.lanes.get(id);
      if (lane && node.status === "running") {
        lane.status.textContent = `cooking ${formatTime(now() - node.startAt)}`;
      }
    }
  }
  requestAnimationFrame(tick);
}

// ---------- The graph ----------

const SVG = "http://www.w3.org/2000/svg";
const svg = (name, attrs = {}, parent) => {
  const el = document.createElementNS(SVG, name);
  for (const [k, v] of Object.entries(attrs)) el.setAttribute(k, v);
  parent?.appendChild(el);
  return el;
};

function layoutGraph({ start, nodes, edges }) {
  const byId = new Map(nodes.map((n) => [n.id, { ...n, layer: 0 }]));
  const out = new Map(nodes.map((n) => [n.id, []]));
  edges.forEach((e) => out.get(e.source)?.push(e));

  // Edges that point back up the graph (the name pick loop) are drawn as a return loop.
  const visit = new Map();
  const dfs = (id) => {
    visit.set(id, 1);
    for (const e of out.get(id) ?? []) {
      if (visit.get(e.sink) === 1) e.back = true;
      else if (!visit.has(e.sink)) dfs(e.sink);
    }
    visit.set(id, 2);
  };
  dfs(start);

  const forward = edges.filter((e) => !e.back);
  for (let i = 0; i < nodes.length; i++) {
    for (const e of forward) {
      const source = byId.get(e.source), sink = byId.get(e.sink);
      if (source && sink) sink.layer = Math.max(sink.layer, source.layer + 1);
    }
  }

  const layers = [];
  for (const n of byId.values()) (layers[n.layer] ??= []).push(n);

  const width = 1600, mid = 78;
  const step = (width - 160) / Math.max(1, layers.length - 1);
  layers.forEach((layer, i) => {
    const main = layer.filter((n) => n.kind !== "human");
    main.forEach((n, j) => {
      n.x = 80 + i * step;
      n.y = mid + (j - (main.length - 1) / 2) * 62;
    });
    layer.filter((n) => n.kind === "human").forEach((n) => {
      n.x = 80 + i * step;
      n.y = mid + 92;
    });
  });

  return { nodes: [...byId.values()], edges, byId, width, height: 200 };
}

const halfWidth = (n) => (n.kind === "agent" ? 78 : n.kind === "human" ? 58 : 9);

function edgePath(e, byId) {
  const a = byId.get(e.source), b = byId.get(e.sink);
  if (e.back) {
    const sx = a.x - halfWidth(a), sy = a.y;
    return `M${sx},${sy} C${sx - 70},${sy} ${b.x},${b.y + 70} ${b.x},${b.y + 12}`;
  }
  const sx = a.x + halfWidth(a), tx = b.x - halfWidth(b);
  const dx = Math.max(30, (tx - sx) / 2);
  return `M${sx},${a.y} C${sx + dx},${a.y} ${tx - dx},${b.y} ${tx},${b.y}`;
}

function drawGraph(graph) {
  const root = $("#graph");
  root.replaceChildren();
  root.setAttribute("viewBox", `0 -10 ${graph.width} ${graph.height + 20}`);

  const edgeLayer = svg("g", {}, root);
  graph.edges.forEach((e) => {
    e.el = svg("path", { class: `edge${e.back ? " back" : ""}`, d: edgePath(e, graph.byId) }, edgeLayer);
  });

  state.nodes.clear();
  graph.nodes.forEach((n) => {
    const g = svg("g", { class: `node ${n.kind}`, transform: `translate(${n.x},${n.y})` }, root);
    const node = { ...n, el: g, status: "idle", startAt: null, endAt: null, timer: null };

    if (n.kind === "code") {
      svg("circle", { class: "halo", r: 9 }, g);
      svg("circle", { class: "shape", r: 9 }, g);
      svg("text", { class: "name", y: 30 }, g).textContent = LABELS[n.id] ?? n.id;
    } else {
      const w = halfWidth(n) * 2, h = n.kind === "agent" ? 44 : 38;
      svg("rect", { class: "halo", x: -w / 2, y: -h / 2, width: w, height: h, rx: h / 2 }, g);
      svg("rect", { class: "shape", x: -w / 2, y: -h / 2, width: w, height: h, rx: h / 2 }, g);
      svg("text", { class: "name", y: 5 }, g).textContent = LABELS[n.id] ?? n.id;
      if (n.kind === "agent") {
        node.timer = svg("text", { class: "timer", y: 40 }, g);
      }
    }

    state.nodes.set(n.id, node);
  });
}

function setNode(id, status, at) {
  const node = state.nodes.get(id);
  if (!node) return;
  if (status === "running" && node.startAt == null) node.startAt = at;
  if (status === "done" || status === "failed") node.endAt = at;
  node.status = status;
  node.el.classList.remove("running", "done", "waiting", "failed");
  if (status !== "idle") node.el.classList.add(status);

  if (status === "running" || status === "waiting") {
    for (const e of state.graph.edges.filter((e) => e.sink === id && !e.back)) {
      e.el.classList.add("hot", "lit");
      clearTimeout(e.cool);
      e.cool = setTimeout(() => e.el.classList.remove("hot"), 1600);
    }
  }
}

// ---------- Tickets ----------

function buildRail() {
  const rail = $("#rail");
  rail.replaceChildren();
  state.lanes.clear();
  for (const station of STATIONS) {
    const ticket = document.createElement("section");
    ticket.className = "ticket";
    ticket.dataset.state = "idle";
    ticket.innerHTML = `
      <header class="ticket-head">
        <h3>${LABELS[station.id]}</h3>
        <span class="status">on hold</span>
        <p class="role">${station.role}</p>
      </header>
      <ul class="tools"></ul>
      <div class="shots"></div>
      <div class="ticket-body md" data-empty="${station.empty}"></div>`;
    rail.appendChild(ticket);
    state.lanes.set(station.id, {
      ticket,
      status: ticket.querySelector(".status"),
      tools: ticket.querySelector(".tools"),
      shots: ticket.querySelector(".shots"),
      body: ticket.querySelector(".ticket-body"),
      text: "",
      calls: new Map(),
      pending: false,
    });
  }
}

function setLane(id, status) {
  const lane = state.lanes.get(id);
  if (!lane) return;
  if (lane.ticket.dataset.state === "idle" && status !== "idle") {
    lane.ticket.classList.add("printing");
    lane.body.dataset.empty = "On it…";
  }
  lane.ticket.dataset.state = status;
  const node = state.nodes.get(id);
  if (status === "done") lane.status.textContent = `plated in ${formatTime((node?.endAt ?? 0) - (node?.startAt ?? 0))}`;
  if (status === "failed") lane.status.textContent = "86'd";
}

function appendText(id, text) {
  const lane = state.lanes.get(id);
  if (!lane) return;
  lane.text += text;
  if (lane.pending) return;
  lane.pending = true;
  requestAnimationFrame(() => {
    lane.pending = false;
    const body = lane.body;
    const follow = body.scrollHeight - body.scrollTop - body.clientHeight < 80;
    body.innerHTML = renderMarkdown(lane.text);
    if (follow) body.scrollTop = body.scrollHeight;
  });
}

function summarizeArgs(args) {
  if (!args) return "";
  const value = args.query ?? args.url ?? args.kind ?? args.prompt ?? Object.values(args)[0];
  return typeof value === "string" ? value : JSON.stringify(value ?? "");
}

function addTool(id, { callId, name, arguments: args }) {
  const lane = state.lanes.get(id);
  if (!lane || lane.calls.has(callId)) return;
  const chip = document.createElement("li");
  chip.className = "tool pending";
  chip.title = JSON.stringify(args ?? {});
  chip.innerHTML = `<b>${escapeHtml(name)}</b> ${escapeHtml(summarizeArgs(args))}`;
  lane.tools.appendChild(chip);
  lane.calls.set(callId, chip);
}

function addToolResult(id, { callId, result }) {
  const lane = state.lanes.get(id);
  const chip = lane?.calls.get(callId);
  chip?.classList.replace("pending", "done");
  const url = result && typeof result === "object" ? result.url ?? result.Url : null;
  if (lane && typeof url === "string") {
    const img = document.createElement("img");
    img.src = localize(url);
    img.alt = result.kind ?? "generated image";
    img.title = result.kind ?? "";
    lane.shots.appendChild(img);
  }
}

// ---------- The name pick ----------

function showCall(options, recommended) {
  const list = $("#options");
  list.classList.remove("chosen");
  list.replaceChildren();
  options.forEach((option, i) => {
    const card = document.createElement("button");
    card.type = "button";
    card.className = "option";
    card.dataset.name = option.name;
    const logo = isImage(localize(option.logoUrl))
      ? `<img src="${escapeHtml(localize(option.logoUrl))}" alt="Logo for ${escapeHtml(option.name)}">`
      : escapeHtml(option.name);
    card.innerHTML = `
      <span class="key" aria-hidden="true">${i + 1}</span>
      ${option.name === recommended ? `<span class="fav">Designer's pick</span>` : ""}
      <span class="logo">${logo}</span>
      <span class="name">${escapeHtml(option.name)}</span>
      <span class="why">${escapeHtml(option.rationale ?? "")}</span>`;
    card.addEventListener("click", () => pick(option.name));
    list.appendChild(card);
  });
  $("#call").hidden = false;
  list.querySelector(".option")?.focus();
}

async function pick(name) {
  const list = $("#options");
  if (list.classList.contains("chosen")) return;
  list.classList.add("chosen");
  list.querySelector(`.option[data-name="${CSS.escape(name)}"]`)?.classList.add("picked");
  const response = await fetch(`/board/api/runs/${state.runId}/choice`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ name }),
  });
  if (!response.ok) {
    list.classList.remove("chosen");
    ticker(`Couldn't send the pick: ${await response.text()}`);
  }
}

function namePicked(name) {
  const card = $(`#options .option[data-name="${CSS.escape(name)}"]`);
  $("#options").classList.add("chosen");
  card?.classList.add("picked");
  state.picked = { name, logo: card?.querySelector(".logo img")?.getAttribute("src") ?? null };
  ticker(`${name} it is.`);
  setTimeout(() => ($("#call").hidden = true), 1400);
}

document.addEventListener("keydown", (e) => {
  if ($("#call").hidden) return;
  const card = $("#options").children[Number(e.key) - 1];
  if (card) card.click();
});

// ---------- The concept ----------

function showConcept() {
  if (!state.concept) return;
  $("#concept-name").textContent = state.picked?.name ?? "The concept";
  const logo = $("#concept-logo");
  logo.replaceChildren();
  if (state.picked?.logo) {
    const img = document.createElement("img");
    img.src = state.picked.logo;
    img.alt = `Logo for ${state.picked.name}`;
    logo.appendChild(img);
  }
  $("#concept-body").innerHTML = renderMarkdown(state.concept);
  $("#concept").hidden = false;
  $("#show-concept").hidden = true;
  $("#back").focus();
}

$("#back").addEventListener("click", () => {
  $("#concept").hidden = true;
  $("#show-concept").hidden = false;
});
$("#show-concept").addEventListener("click", showConcept);

// ---------- Events ----------

const ticker = (text) => ($("#ticker").textContent = text);

function handle(evt) {
  const { type, at, executor, data } = evt;
  state.base = Math.min(state.base, Date.now() - at);

  switch (type) {
    case "run-started":
      $("#idea").value = data.idea;
      document.body.dataset.state = "running";
      ticker("Order in. Firing the researcher.");
      break;

    case "executor-invoked":
      setNode(executor, "running", at);
      if (state.lanes.has(executor)) {
        setLane(executor, "running");
        ticker(`${LABELS[executor] ?? executor} is cooking.`);
      }
      else if (executor !== "intake") ticker(`${LABELS[executor] ?? executor} is passing plates along.`);
      break;

    case "executor-completed": {
      const lane = state.lanes.get(executor);
      // Agents are invoked twice (the messages, then the turn token): only the turn with output finishes them.
      if (lane && !lane.text) break;
      setNode(executor, "done", at);
      if (lane) setLane(executor, "done");
      break;
    }

    case "executor-failed":
      setNode(executor, "failed", at);
      setLane(executor, "failed");
      ticker(`${LABELS[executor] ?? executor} failed: ${data?.error ?? "unknown error"}`);
      break;

    case "text":
      if (state.lanes.has(executor)) appendText(executor, data.text);
      break;

    case "tool-call":
      addTool(executor, data);
      break;

    case "tool-result":
      addToolResult(executor, data);
      break;

    case "pick-name":
      setNode(executor, "waiting", at);
      document.body.dataset.state = "waiting";
      ticker("Your call: name the restaurant.");
      showCall(data.options, data.recommended);
      break;

    case "name-picked":
      setNode(executor, "done", at);
      document.body.dataset.state = "running";
      namePicked(data.name);
      break;

    case "output":
      // Only the presenter's output is the concept (the other agents emit their answers as outputs too).
      if (data.text && executor === "presenter") state.concept = data.text;
      break;

    case "run-completed":
      finish(at);
      state.concept ??= state.lanes.get("presenter")?.text || null;
      if (state.concept) {
        ticker("Service! The concept is plated.");
        setTimeout(showConcept, 600);
      } else {
        ticker("The run ended.");
      }
      break;

    case "run-failed":
      finish(at);
      ticker(`The run failed: ${data?.error ?? "unknown error"}`);
      break;
  }
}

function finish(at) {
  state.endAt = at;
  document.body.dataset.state = "done";
  state.source?.close();
  for (const [id, node] of state.nodes) {
    if (node.status === "running") setNode(id, "done", at);
  }
}

function follow(runId) {
  state.runId = runId;
  state.seen = 0;
  $("#reset").hidden = false;
  $("#fire").disabled = true;
  $("#idea").readOnly = true;
  history.replaceState(null, "", `#run=${runId}`);

  const source = new EventSource(`/board/api/runs/${runId}/events`);
  state.source = source;
  source.onopen = () => (state.received = 0);
  source.onmessage = (message) => {
    const evt = JSON.parse(message.data);
    if (evt.type === "clock") {
      state.base = Math.min(state.base, Date.now() - evt.at);
      return;
    }

    // A reconnect replays the run from the start: skip what was handled already.
    state.received++;
    if (state.received <= state.seen) return;
    state.seen = state.received;
    handle(evt);
  };
  source.onerror = () => {
    if (document.body.dataset.state === "done") source.close();
  };
}

$("#order").addEventListener("submit", async (e) => {
  e.preventDefault();
  const idea = $("#idea").value.trim();
  if (!idea) return;
  $("#fire").disabled = true;
  const response = await fetch("/board/api/runs", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ idea }),
  });
  if (!response.ok) {
    $("#fire").disabled = false;
    ticker(`Couldn't start the run: ${await response.text()}`);
    return;
  }
  follow((await response.json()).id);
});

$("#reset").addEventListener("click", () => {
  state.source?.close();
  history.replaceState(null, "", location.pathname);
  location.reload();
});

// ---------- Start ----------

async function init() {
  buildRail();
  const response = await fetch("/board/api/graph");
  state.graph = layoutGraph(await response.json());
  drawGraph(state.graph);
  requestAnimationFrame(tick);

  const run = new URLSearchParams(location.hash.slice(1)).get("run");
  if (run) follow(run);
  else $("#idea").focus();
}

init().catch((error) => ticker(`The board couldn't load: ${error.message}`));
