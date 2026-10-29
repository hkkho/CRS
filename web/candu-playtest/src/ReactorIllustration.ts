/** Original cover art, intentionally decorative: the live board uses shared topology. */
export function reactorIllustration(): string {
  const ports = Array.from({ length: 22 }, (_, row) => Array.from({ length: 22 }, (_, col) => {
    const x = (col - 10.5) * 16, y = (row - 10.5) * 16;
    if (x * x + y * y > 176 * 176) return "";
    const hot = (row * 7 + col * 3) % 13 < 3;
    return `<g transform="translate(${300 + x} ${260 + y})"><circle r="6.3" fill="${hot ? "#f7b54d" : "#638680"}" stroke="${hot ? "#ffdc94" : "#a5bab0"}"/><circle r="3.1" fill="#17282b"/><path d="M-2-2h4" stroke="${hot ? "#d99b42" : "#72978a"}" stroke-width="1.5"/></g>`;
  }).join("")).join("");
  return `<svg viewBox="0 0 640 620" role="img" aria-label="Illustrated steel reactor with amber fuel channels">
    <defs>
      <linearGradient id="cover-steel" x2=".8" y2="1"><stop stop-color="#c0c5ad"/><stop offset=".2" stop-color="#69817a"/><stop offset=".48" stop-color="#273c40"/><stop offset=".72" stop-color="#95a394"/><stop offset="1" stop-color="#344b4b"/></linearGradient>
      <radialGradient id="cover-face" cx=".4" cy=".3"><stop stop-color="#354b47"/><stop offset="1" stop-color="#13262b"/></radialGradient>
      <pattern id="cover-grain" width="4" height="4" patternUnits="userSpaceOnUse"><path d="M0 1h4" stroke="#e1e6ca" stroke-opacity=".08"/></pattern>
      <pattern id="cover-stripes" width="16" height="16" patternUnits="userSpaceOnUse" patternTransform="rotate(45)"><rect width="8" height="16" fill="#f2b74d"/></pattern>
    </defs>
    <ellipse cx="336" cy="538" rx="247" ry="35" fill="#030b0e" opacity=".6"/>
    <g fill="none" stroke="#364b4e" stroke-width="16"><path d="M53 468V307q0-27 28-27h49M563 462V230q0-30-30-30h-30"/><path d="M96 468V380h36"/></g>
    <g fill="none" stroke="#a5b09a" stroke-width="3"><path d="M53 468V307q0-27 28-27h49M563 462V230q0-30-30-30h-30"/></g>
    <path d="M167 431l-19 73h372l-36-73" fill="#263b3f" stroke="#73867c" stroke-width="2"/>
    <path d="M112 500h431v28H112z" fill="#182c32" stroke="#51675f"/><path d="M121 507h413v10H121z" fill="url(#cover-stripes)"/>
    <g transform="translate(26 24)"><circle cx="310" cy="274" r="221" fill="#101e24" stroke="#536b64" stroke-width="3"/><circle cx="310" cy="274" r="210" fill="none" stroke="#34494a" stroke-width="12"/>
      ${Array.from({ length: 12 }, (_, i) => `<path d="M${310 + Math.cos(i * Math.PI / 6) * 214} ${274 + Math.sin(i * Math.PI / 6) * 214}l-24-23" stroke="#819086" stroke-width="6"/>`).join("")}
    </g>
    <circle cx="300" cy="260" r="222" fill="url(#cover-steel)" stroke="#bec6ab" stroke-width="2"/>
    <circle cx="300" cy="260" r="204" fill="url(#cover-face)" stroke="#08171d" stroke-width="5"/>
    <circle cx="300" cy="260" r="196" fill="none" stroke="#c2cab0" stroke-opacity=".2"/>
    <circle cx="300" cy="260" r="222" fill="url(#cover-grain)"/>
    ${Array.from({ length: 24 }, (_, i) => { const a = i * Math.PI / 12; return `<g transform="translate(${300 + Math.cos(a) * 213} ${260 + Math.sin(a) * 213}) rotate(${i * 15})"><path d="M-4-2l4-2 4 2v4l-4 2-4-2z" fill="#182e33" stroke="#c1c6ad" stroke-width=".6"/><path d="M-2 0h4" stroke="#8a9c8b"/></g>`; }).join("")}
    ${ports}
    <g transform="translate(24 308)"><rect width="62" height="65" rx="3" fill="#edae43" stroke="#ffe2a1"/><rect x="7" y="8" width="48" height="40" fill="#293b3d"/><path d="M18 16v23m9-23v23m9-23v23m9-23v23" stroke="#a3b2a1" stroke-width="4"/><path d="M62 20h22v24H62" fill="#b9c3ab"/></g>
    <g fill="#a8b9ae" font-family="monospace" font-size="10" letter-spacing="2"><text x="100" y="572">CANDU / 06</text><text x="540" y="572" text-anchor="end">ON-POWER REFUELLING</text></g>
    <g fill="none" stroke="#edb44e"><path d="M469 118l50-44h78M190 419l-38 43H28"/><circle cx="469" cy="118" r="11"/><circle cx="190" cy="419" r="11"/></g>
    <g fill="#f4ca77" font-family="monospace" font-size="10"><text x="521" y="63">THE CORE</text><text x="28" y="482">YOUR NEXT MOVE</text></g>
  </svg>`;
}
