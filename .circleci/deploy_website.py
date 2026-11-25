"""Publish a generated site to the dedicated GitHub Pages branch."""
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

from build_website import SOURCE, fetch_releases, render

REPOSITORY = 'https://github.com/JimMarley420/Preset_launcher.git'
BRANCH = 'gh-pages'


def git(directory, *args, env=None, check=True):
    return subprocess.run(['git', '-C', str(directory), '-c', 'credential.helper=', *args],
                          env=env, check=check, text=True, capture_output=True)


def remote_sha(directory, branch, env):
    result = git(directory, 'ls-remote', '--exit-code', REPOSITORY,
                 f'refs/heads/{branch}', env=env, check=False)
    if result.returncode == 2:
        return None
    result.check_returncode()
    return result.stdout.split()[0]


def sync_output(output, checkout):
    for path in checkout.iterdir():
        if path.name == '.git':
            continue
        if path.is_dir() and not path.is_symlink():
            shutil.rmtree(path)
        else:
            path.unlink()
    shutil.copytree(output, checkout, dirs_exist_ok=True)


def main():
    from release import API, request
    if os.environ.get('CIRCLE_BRANCH') != 'main':
        raise SystemExit('Website deployment requires main')
    source_sha = git(SOURCE.parent, 'rev-parse', 'HEAD').stdout.strip()
    if source_sha != os.environ.get('CIRCLE_SHA1'):
        raise SystemExit('Website checkout does not match pipeline commit')
    token = os.environ['GH_PAGES_TOKEN']
    if not token.strip():
        raise SystemExit('GH_PAGES_TOKEN must not be empty')
    with tempfile.TemporaryDirectory() as temp:
        work = Path(temp)
        askpass = work / 'askpass.sh'
        askpass.write_text('#!/bin/sh\ncase "$1" in\n  *Username*) printf "%s\\n" x-access-token;;\n  *) printf "%s\\n" "$GH_PAGES_TOKEN";;\nesac\n')
        askpass.chmod(0o700)
        env = dict(os.environ, GIT_ASKPASS=str(askpass), GIT_TERMINAL_PROMPT='0', LC_ALL='C')
        if remote_sha(SOURCE.parent, 'main', env) != source_sha:
            print('Main advanced; newer pipeline will publish the site.')
            return
        latest, releases = fetch_releases(request, API, token)
        output = work / 'site'
        render(SOURCE, output, latest, releases)
        checkout = work / 'pages'
        checkout.mkdir()
        git(checkout, 'init', '--initial-branch=' + BRANCH)
        git(checkout, 'config', 'user.name', 'JimMarley420')
        git(checkout, 'config', 'user.email', '186522763+JimMarley420@users.noreply.github.com')
        previous = remote_sha(checkout, BRANCH, env)
        if previous:
            git(checkout, 'fetch', '--no-tags', '--depth=1', REPOSITORY, f'refs/heads/{BRANCH}', env=env)
            git(checkout, 'checkout', '-B', BRANCH, 'FETCH_HEAD')
        sync_output(output, checkout)
        git(checkout, 'add', '--all')
        if not git(checkout, 'status', '--porcelain').stdout:
            print('Website is already up to date.')
            return
        identity = 'JimMarley420 <186522763+JimMarley420@users.noreply.github.com>'
        for role in ('GIT_AUTHOR_IDENT', 'GIT_COMMITTER_IDENT'):
            if not git(checkout, 'var', role).stdout.startswith(identity + ' '):
                raise ValueError('Unexpected Git commit identity')
        git(checkout, 'commit', '-m', f'docs: publish website from {source_sha[:12]}')
        if remote_sha(checkout, 'main', env) != source_sha:
            print('Main advanced; newer pipeline will publish the site.')
            return
        # Normal fast-forward push: concurrent external changes fail rather than being overwritten.
        git(checkout, 'push', REPOSITORY, f'HEAD:refs/heads/{BRANCH}', env=env)
        print(f'Published {latest["tag_name"]} website from {source_sha}.')


if __name__ == '__main__':
    main()
