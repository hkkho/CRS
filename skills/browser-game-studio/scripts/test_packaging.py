"""Verify reusable packaging and HTTP module behavior without a browser driver."""

from functools import partial
from http.server import ThreadingHTTPServer
import json
from pathlib import Path
from tempfile import TemporaryDirectory
from threading import Thread
import unittest
from urllib.request import Request, urlopen

from create_game import create
from serve import GameHandler


class PackagingTests(unittest.TestCase):
    def test_seed_is_playable_and_existing_work_is_preserved(self):
        with TemporaryDirectory() as directory:
            output = Path(directory) / 'game'
            create(output, 'factory', 'Copper Moon', 73)
            project = json.loads((output / 'project.json').read_text())
            self.assertEqual(project['pattern'], 'factory')
            self.assertEqual(project['seed'], 73)
            self.assertEqual(project['mode'], 'game')
            self.assertTrue((output / 'index.html').is_file())
            self.assertTrue((output / 'serve.py').is_file())
            marker = output / 'player-work.txt'
            marker.write_text('Keep this work.', encoding='utf-8')
            with self.assertRaises(FileExistsError):
                create(output, 'garden', 'Replacement', 42)
            self.assertEqual(marker.read_text(), 'Keep this work.')
            self.assertEqual(json.loads((output / 'project.json').read_text()), project)

    def test_invalid_inputs_create_nothing(self):
        with TemporaryDirectory() as directory:
            output = Path(directory) / 'game'
            for pattern, title, seed in [('unknown', 'Title', 1),
                                         ('post', ' ', 1), ('post', 'Title', -1),
                                         ('post', 'Title', 2**32)]:
                with self.assertRaises(ValueError):
                    create(output, pattern, title, seed)
                self.assertFalse(output.exists())

    def test_modules_keep_valid_mime_after_a_conditional_request(self):
        with TemporaryDirectory() as directory:
            (Path(directory) / 'entry.mjs').write_text('export const ready = true;')
            server = ThreadingHTTPServer(('127.0.0.1', 0),
                                         partial(GameHandler, directory=directory))
            thread = Thread(target=server.serve_forever, daemon=True)
            thread.start()
            try:
                url = f'http://127.0.0.1:{server.server_port}/entry.mjs'
                request = Request(url, headers={
                    'If-Modified-Since': 'Wed, 31 Dec 2098 23:59:59 GMT'})
                with urlopen(request) as response:
                    self.assertEqual(response.status, 200)
                    self.assertEqual(response.headers.get_content_type(), 'text/javascript')
                    self.assertEqual(response.headers['Cache-Control'], 'no-store')
                    self.assertIn(b'export const ready', response.read())
            finally:
                server.shutdown()
                server.server_close()
                thread.join()


if __name__ == '__main__':
    unittest.main()
