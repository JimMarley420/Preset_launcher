"""Render public GitHub release data into the static site before publication."""
from datetime import datetime
from html import escape
from pathlib import Path
import argparse
import os
import re
import shutil
from urllib.parse import quote

REPO = 'https://github.com/JimMarley420/Preset_launcher'
SOURCE = Path(__file__).resolve().parents[1] / 'website'
VERSION = re.compile(r'v?\d+\.\d+\.\d+\Z')
ARCHITECTURES = (
    ('x64', '', 'Intel / AMD 64-bit', 'Intel / AMD 64 bits', 'Tested natively', 'Testé nativement'),
    ('x86', '_x86', '32-bit Windows', 'Windows 32 bits', 'Tested under WOW64', 'Testé sous WOW64'),
    ('ARM64', '_arm64', 'Windows on ARM', 'Windows sur ARM', 'Native testing pending', 'Tests natifs en attente'),
)
MONTHS = ('janvier février mars avril mai juin juillet août septembre octobre novembre décembre').split()


def stable(release):
    return (not release['draft'] and not release['prerelease']
            and bool(VERSION.fullmatch(release['tag_name'])))


def tag_url(release):
    return REPO + '/releases/tag/' + quote(release['tag_name'], safe='')


def asset_url(release, asset):
    url = REPO + '/releases/download/' + quote(release['tag_name'], safe='') + '/' + quote(asset['name'], safe='')
    if asset['browser_download_url'] != url or not isinstance(asset['size'], int) or asset['size'] <= 0:
        raise ValueError('Invalid release asset metadata')
    return url


def date_label(release, fr):
    date = datetime.fromisoformat(release['published_at'].replace('Z', '+00:00'))
    return f'{date.day} {MONTHS[date.month - 1]} {date.year}' if fr else date.strftime('%d %B %Y').lstrip('0')


def link(url, label, css='text-link'):
    return f'<a class="{css}" href="{escape(url, quote=True)}">{escape(label)}</a>'


def release_downloads(release, fr):
    version = release['tag_name'].removeprefix('v')
    assets = {asset['name']: asset for asset in release['assets']}
    rows = []
    for arch, suffix, en_label, fr_label, en_test, fr_test in ARCHITECTURES:
        asset = assets.get(f'PresetLauncher_Setup_{version}{suffix}.exe')
        if asset is None:
            continue
        label, test = (fr_label, fr_test) if fr else (en_label, en_test)
        size = f'{asset["size"] / 1_000_000:.1f}'
        if fr:
            size = size.replace('.', ',')
        css = 'button primary' if arch == 'x64' else 'button'
        button = link(asset_url(release, asset), 'Télécharger' if fr else 'Download', css)
        pending = ' pending' if arch == 'ARM64' else ''
        rows.append(f'<div class="download-row"><div><h3>Windows {arch}</h3><p>{escape(label)} · {size} {"Mo" if fr else "MB"} · EXE</p></div><span class="validation{pending}">{escape(test)}</span>{button}</div>')
    if not rows:
        raise ValueError('Latest stable release contains no supported installer')
    links = []
    if 'SHA256SUMS.txt' in assets:
        links.append(link(asset_url(release, assets['SHA256SUMS.txt']), 'Sommes SHA-256' if fr else 'SHA-256 checksums'))
    links.extend((link(REPO + '/releases', 'Toutes les versions' if fr else 'All releases'),
                  link('changelog.html', 'Historique des versions' if fr else 'Changelog')))
    notes = link(tag_url(release), 'Notes de version' if fr else 'Release notes')
    return (f'<div class="release-head"><h2>Version {escape(version)}</h2><span>{date_label(release, fr)}</span>{notes}</div>'
            '<div class="download-list">' + ''.join(rows) + '</div><div class="resource-links">' + ''.join(links) + '</div>')


def release_history(releases, fr):
    entries = []
    for release in releases:
        # Keep only public release notes, not the signed updater's hidden metadata.
        notes = re.sub(r'<!--.*?(?:-->|\Z)', '', release.get('body') or '', flags=re.S).strip()
        # Show the actual Markdown as readable text without executing remote HTML.
        notes = re.sub(r'(?m)^#{1,6}\s+', '', notes)
        notes = re.sub(r'(?m)^\* ', '• ', notes).replace('**', '')
        title = escape(release['tag_name'])
        body = f'<div class="release-notes">{escape(notes)}</div>' if notes else ''
        entries.append(f'<article class="release-entry"><div class="release-head"><h2>{title}</h2><span>{date_label(release, fr)}</span></div>{body}'
                       + link(tag_url(release), 'Version sur GitHub' if fr else 'Release on GitHub') + '</article>')
    return ''.join(entries) + '<div class="resource-links">' + link(REPO + '/releases', 'Toutes les versions' if fr else 'All releases') + '</div>'


def substitute(html, key, value):
    pattern = re.compile(r'<!-- ' + key + r' -->.*?<!-- /' + key + r' -->', re.S)
    result, count = pattern.subn(lambda _: value, html)
    if count != 1:
        raise ValueError(f'Expected exactly one {key} placeholder, found {count}')
    return result


def render(source, output, latest, releases):
    if not stable(latest):
        raise ValueError('Latest release is not stable')
    history = sorted((r for r in releases if stable(r)), key=lambda r: r['published_at'], reverse=True)
    if not any(r['tag_name'] == latest['tag_name'] for r in history):
        raise ValueError('Latest release missing from release history')
    if output.exists():
        raise ValueError('Output directory must not already exist')
    shutil.copytree(source, output)
    for page in output.rglob('*.html'):
        fr = page.parent.name == 'fr'
        tag = escape(latest['tag_name'])
        banner = f'PresetLauncher {tag.removeprefix("v")} est disponible.' if fr else f'PresetLauncher {tag.removeprefix("v")} is available.'
        html = substitute(page.read_text(encoding='utf-8'), 'RELEASE_BANNER', banner)
        if page.name == 'index.html':
            html = substitute(html, 'CURRENT_VERSION', link(tag_url(latest), latest['tag_name']))
        elif page.name == 'download.html':
            html = substitute(html, 'RELEASE_DOWNLOADS', release_downloads(latest, fr))
        elif page.name == 'changelog.html':
            html = substitute(html, 'RELEASE_HISTORY', release_history(history, fr))
        page.write_text(html, encoding='utf-8')
    (output / '.nojekyll').touch()


def fetch_releases(request, api, token):
    latest = request(api + '/releases/latest', token=token)
    releases = []
    for page in range(1, 101):
        batch = request(api + f'/releases?per_page=100&page={page}', token=token)
        releases.extend(batch)
        if len(batch) < 100:
            return latest, releases
    raise ValueError('Release history exceeded pagination limit')


def main():
    from release import API, request
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    latest, releases = fetch_releases(request, API, os.environ.get('GH_PAGES_TOKEN'))
    render(SOURCE, args.output, latest, releases)


if __name__ == '__main__':
    main()
