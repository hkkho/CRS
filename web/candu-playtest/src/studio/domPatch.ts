/** Patch stable SVG structure without replacing focused/inspected nodes. */
export function patchMarkup(parent: Element, markup: string): void {
  const template = document.createElement('template'); template.innerHTML = markup;
  const patch = (current: Node, next: Node): void => {
    if (current.nodeType !== next.nodeType || current.nodeName !== next.nodeName) {
      current.parentNode!.replaceChild(next.cloneNode(true), current); return;
    }
    if (current instanceof Element && next instanceof Element) {
      for (const attr of [...current.attributes]) if (!next.hasAttribute(attr.name)) current.removeAttribute(attr.name);
      for (const attr of [...next.attributes]) setAttribute(current, attr.name, attr.value);
    } else if (current.nodeValue !== next.nodeValue) current.nodeValue = next.nodeValue;
    const children = [...next.childNodes];
    children.forEach((child, index) => current.childNodes[index] ? patch(current.childNodes[index], child) : current.appendChild(child.cloneNode(true)));
    while (current.childNodes.length > children.length) current.lastChild!.remove();
  };
  [...template.content.childNodes].forEach((child, index) => parent.childNodes[index] ? patch(parent.childNodes[index], child) : parent.appendChild(child.cloneNode(true)));
  while (parent.childNodes.length > template.content.childNodes.length) parent.lastChild!.remove();
}
export function setAttribute(element: Element, name: string, value: string): void {
  if (element.getAttribute(name) !== value) element.setAttribute(name, value);
}
