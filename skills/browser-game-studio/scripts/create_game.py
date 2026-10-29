"""Copy one original playable pattern into a new standalone prototype folder."""

import argparse
import json
from pathlib import Path
import shutil

PATTERNS = ('council', 'post', 'water', 'harvest', 'flock', 'factory', 'garden', 'evolution', 'orb', 'hamlet')


def create(output, pattern, title, seed):
    if pattern not in PATTERNS or not 0 <= seed <= 0xffffffff or not title.strip():
        raise ValueError('Choose a pattern, nonempty title and uint32 seed.')
    source = Path(__file__).resolve().parents[1] / 'assets' / 'pattern-lab'
    target = Path(output).resolve()
    if target.exists():
        raise FileExistsError(f'Output already exists; no files were changed: {target}')
    if target.is_relative_to(source):
        raise ValueError('Output cannot be inside the template source.')
    shutil.copytree(source, target)
    shutil.copy2(Path(__file__).with_name('serve.py'), target / 'serve.py')
    (target / 'project.json').write_text(json.dumps({
        'mode': 'game', 'title': title.strip(), 'pattern': pattern, 'seed': seed,
    }, indent=2) + '\n', encoding='utf-8')
    return target


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    parser.add_argument('--pattern', choices=PATTERNS, required=True)
    parser.add_argument('--title', required=True)
    parser.add_argument('--seed', type=int, default=42)
    args = parser.parse_args()
    try:
        target = create(args.output, args.pattern, args.title, args.seed)
    except (ValueError, FileExistsError) as error:
        parser.error(str(error))
    print(json.dumps({'created': str(target), 'pattern': args.pattern,
                      'entry': 'index.html', 'scope': 'original prototype seed; develop and playtest before release'}))
