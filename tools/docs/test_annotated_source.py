"""Focused checks for source extraction, line coverage and failure behavior."""

from copy import deepcopy
from pathlib import Path
import tempfile
import unittest

from annotated_source import render_excerpt


class AnnotatedSourceTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        root = Path(self.directory.name)
        (root / "example.ts").write_text(
            '// context\n  function run() {\n    return "<ready>";\n  }\n// after\n', encoding="utf-8")
        self.config = {"config_file_path": str(root / "mkdocs.yml"),
                       "repo_url": "https://example.com/repository"}
        self.spec = {"path": "example.ts", "title": "A <label>", "language": "typescript",
                     "start": "function run() {", "end": 'return "<ready>";\n}',
                     "steps": [{"through": "function run() {", "explanation": "**Declare** a function."},
                               {"through": 'return "<ready>";\n}', "explanation": "Return `text`."}]}

    def test_extracts_real_lines_and_renders_paired_explanations(self):
        rendered = render_excerpt(self.spec, self.config)
        self.assertIn('example.ts#L2-L4', rendered)
        self.assertIn('Lines 2–2', rendered)
        self.assertIn('Lines 3–4', rendered)
        self.assertEqual(rendered.count('class="source-line"'), 3)
        self.assertEqual(rendered.count('class="annotated-step"'), 2)
        self.assertIn('<strong>Declare</strong>', rendered)
        self.assertIn('<code>text</code>', rendered)
        self.assertIn('A &lt;label&gt;', rendered)
        self.assertIn('&lt;ready&gt;', rendered)
        self.assertNotIn('// context', rendered)
        self.assertNotIn('// after', rendered)

    def test_missing_changed_or_reordered_anchors_fail(self):
        for field in ['start', 'end']:
            with self.subTest(field=field):
                spec = deepcopy(self.spec)
                spec[field] = 'missing'
                with self.assertRaises(ValueError):
                    render_excerpt(spec, self.config)
        spec = deepcopy(self.spec)
        spec['steps'].reverse()
        with self.assertRaisesRegex(ValueError, 'wrong order'):
            render_excerpt(spec, self.config)

    def test_duplicate_start_fails_instead_of_selecting_wrong_method(self):
        path = Path(self.directory.name) / 'example.ts'
        path.write_text(path.read_text(encoding='utf-8') + 'function run() {\n}\n', encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'occur once'):
            render_excerpt(self.spec, self.config)

    def test_all_lines_require_nonempty_commentary(self):
        spec = deepcopy(self.spec)
        spec['steps'].pop()
        with self.assertRaisesRegex(ValueError, 'no explanation'):
            render_excerpt(spec, self.config)
        spec = deepcopy(self.spec)
        spec['steps'][0]['explanation'] = '  '
        with self.assertRaisesRegex(ValueError, 'needs an explanation'):
            render_excerpt(spec, self.config)

    def test_source_cannot_escape_repository(self):
        spec = deepcopy(self.spec)
        spec['path'] = '../outside.ts'
        with self.assertRaisesRegex(ValueError, 'within the repository'):
            render_excerpt(spec, self.config)


if __name__ == '__main__':
    unittest.main()
