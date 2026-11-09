// Leaflet inserts a string passed to bindPopup/bindTooltip as HTML. Route names, descriptions
// and station names come from users and from OpenStreetMap, so they must go in as text nodes.

/** A span holding plain text, safe to pass to bindTooltip. */
export function safeTooltipContent(text: string): HTMLElement {
  const el = document.createElement("span");
  el.textContent = text;
  return el;
}

/** A popup with a heading and one line per entry, all set as plain text. */
export function safePopupContent(title: string, lines: string[]): HTMLElement {
  const container = document.createElement("div");
  const heading = document.createElement("h2");
  heading.textContent = title;
  container.appendChild(heading);
  const body = document.createElement("p");
  lines.forEach((line, i) => {
    if (i > 0) body.appendChild(document.createElement("br"));
    body.appendChild(document.createTextNode(line));
  });
  container.appendChild(body);
  return container;
}
