"""Serve a local prototype with explicit JavaScript MIME types on Windows too."""

import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


class GameHandler(SimpleHTTPRequestHandler):
    # Windows registry associations can otherwise make Python return text/plain
    # for .mjs; browsers correctly refuse that MIME type for module scripts.
    extensions_map = {**SimpleHTTPRequestHandler.extensions_map,
                      '.mjs': 'text/javascript', '.js': 'text/javascript',
                      '.json': 'application/json', '.css': 'text/css',
                      '.svg': 'image/svg+xml', '.wasm': 'application/wasm'}

    def do_GET(self):
        # Development previews should always use the current source and MIME
        # headers, including after switching from an incorrectly typed server.
        if 'If-Modified-Since' in self.headers:
            del self.headers['If-Modified-Since']
        super().do_GET()

    def end_headers(self):
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', type=Path, default=Path(__file__).resolve().parent)
    parser.add_argument('--port', type=int, default=8010)
    args = parser.parse_args()
    directory = args.directory.resolve()
    if not directory.is_dir():
        parser.error('Choose an existing directory.')
    server = ThreadingHTTPServer(('127.0.0.1', args.port), partial(GameHandler, directory=str(directory)))
    print(f'Serving {directory} at http://127.0.0.1:{args.port}/', flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
