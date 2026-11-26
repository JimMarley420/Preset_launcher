"""Render public GitHub release data into the static site before publication."""
from datetime import datetime
from html import escape
from pathlib import Path
import argparse
import json
import xml.etree.ElementTree as ET
import os
import re
import shutil
from urllib.parse import quote

REPO = 'https://github.com/JimMarley420/Preset_launcher'
SITE = 'https://jimmarley420.github.io/Preset_launcher/'
# One entry per pair of translated pages; the renderer rejects missing pairs.
SEO = {
    'index.html': (
        'PresetLauncher — Launch Multiple Windows Apps in One Click',
        'Group Windows apps into presets for gaming, work or development. Set launch order and delays. Free and open source.',
        'PresetLauncher — Lancez plusieurs applications Windows en un clic',
        'Regroupez vos applications Windows pour le jeu, le travail ou le développement. Réglez les délais et l’ordre de lancement.'),
    'features.html': (
        'App Launching Features · PresetLauncher',
        'Launch all or only missing programs, set per-app delays, review history and back up your presets.',
        'Fonctionnalités du lanceur Windows · PresetLauncher',
        'Lancez tous les programmes ou seulement ceux qui manquent, configurez les délais et sauvegardez vos presets.'),
    'download.html': (
        'Download PresetLauncher for Windows',
        'Download the latest free Windows x64, x86 or ARM64 installers, when available, with release notes and SHA-256 checksums.',
        'Télécharger PresetLauncher pour Windows',
        'Téléchargez les installateurs Windows x64, x86 ou ARM64 disponibles, avec notes de version et sommes SHA-256.'),
    'docs.html': (
        'How to Launch Multiple Programs · PresetLauncher',
        'Create a Windows app preset, set launch order and delays, back up your data and troubleshoot launch failures.',
        'Comment lancer plusieurs programmes · PresetLauncher',
        'Créez un preset Windows, réglez l’ordre et les délais de lancement, sauvegardez vos données et résolvez les erreurs.'),
    'contribute.html': (
        'Contribute to PresetLauncher',
        'Report bugs, suggest features or contribute to the open-source Windows app launcher on GitHub.',
        'Contribuer à PresetLauncher',
        'Signalez un bug, proposez une fonctionnalité ou contribuez au lanceur Windows open source sur GitHub.'),
    'screenshots.html': (
        'PresetLauncher Screenshots',
        'See the PresetLauncher interface on Windows before installing the free application group launcher.',
        'Captures d’écran de PresetLauncher',
        'Découvrez l’interface Windows de PresetLauncher avant d’installer le lanceur gratuit de groupes d’applications.'),
    'changelog.html': (
        'PresetLauncher Changelog and Releases',
        'Browse stable PresetLauncher releases and their changes, fixes and improvements.',
        'Historique des versions de PresetLauncher',
        'Consultez les versions stables de PresetLauncher, leurs changements, corrections et améliorations.'),
    'security.html': (
        'PresetLauncher Security and Updates',
        'Learn how PresetLauncher verifies updates, handles local data and receives vulnerability reports.',
        'Sécurité et mises à jour de PresetLauncher',
        'Découvrez la vérification des mises à jour, le stockage local et le signalement des vulnérabilités.'),
    'about.html': (
        'About PresetLauncher · Open-source Windows Utility',
        'Learn about PresetLauncher, an independent MIT-licensed Windows launcher built with C# and WinUI 3.',
        'À propos de PresetLauncher · Utilitaire Windows open source',
        'Découvrez PresetLauncher, un lanceur Windows indépendant sous licence MIT développé en C# et WinUI 3.'),
}
SCREENSHOTS = (
    'https://github.com/user-attachments/assets/d874f101-7f59-45f0-b0a0-7ce8702f2735',
    'https://github.com/user-attachments/assets/f793c7ed-9a23-4a1a-a7f7-33bc0f7a3db0',
)
SITEMAP_NS = 'http://www.sitemaps.org/schemas/sitemap/0.9'
XHTML_NS = 'http://www.w3.org/1999/xhtml'
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


def page_url(name, fr):
    return SITE + ('fr/' if fr else '') + ('' if name == 'index.html' else name)


def alternate_urls(name):
    return {'en': page_url(name, False), 'fr': page_url(name, True),
            'x-default': page_url(name, False)}


def search_metadata(html, name, fr):
    title, description = SEO[name][2:] if fr else SEO[name][:2]
    canonical = page_url(name, fr)
    image = SCREENSHOTS[int(fr)]
    html, title_count = re.subn(r'<title>[^<]*</title>',
                                lambda _: f'<title>{escape(title)}</title>', html)
    html, description_count = re.subn(
        r'<meta name="description" content="[^"]*">',
        lambda _: f'<meta name="description" content="{escape(description, quote=True)}">', html)
    if title_count != 1 or description_count != 1 or html.count('</head>') != 1:
        raise ValueError(f'Invalid metadata placeholders in {name}')
    tags = [f'<link rel="canonical" href="{canonical}">']
    tags.extend(f'<link rel="alternate" hreflang="{language}" href="{url}">'
                for language, url in alternate_urls(name).items())
    properties = {'og:type': 'website', 'og:site_name': 'PresetLauncher',
                  'og:title': title, 'og:description': description, 'og:url': canonical,
                  'og:image': image, 'og:image:alt': 'Interface de PresetLauncher' if fr else 'PresetLauncher interface'}
    tags.extend(f'<meta property="{key}" content="{escape(value, quote=True)}">'
                for key, value in properties.items())
    tags.append('<meta name="twitter:card" content="summary_large_image">')
    if name == 'index.html':
        schema = {'@context': 'https://schema.org', '@type': 'SoftwareApplication',
                  'name': 'PresetLauncher', 'description': description, 'url': canonical,
                  'operatingSystem': 'Windows 10, Windows 11', 'applicationCategory': 'UtilitiesApplication',
                  'inLanguage': 'fr' if fr else 'en', 'isAccessibleForFree': True,
                  'offers': {'@type': 'Offer', 'price': 0, 'priceCurrency': 'USD'},
                  'downloadUrl': page_url('download.html', fr), 'screenshot': image,
                  'license': REPO + '/blob/main/LICENSE'}
        tags.append('<script type="application/ld+json">' + json.dumps(schema) + '</script>')
    return html.replace('</head>', '\n' + '\n'.join(tags) + '\n</head>')


def write_sitemap(output):
    ET.register_namespace('', SITEMAP_NS)
    ET.register_namespace('xhtml', XHTML_NS)
    root = ET.Element(f'{{{SITEMAP_NS}}}urlset')
    for name in sorted(SEO):
        for fr in (False, True):
            entry = ET.SubElement(root, f'{{{SITEMAP_NS}}}url')
            ET.SubElement(entry, f'{{{SITEMAP_NS}}}loc').text = page_url(name, fr)
            for language, url in alternate_urls(name).items():
                ET.SubElement(entry, f'{{{XHTML_NS}}}link',
                              rel='alternate', hreflang=language, href=url)
    ET.indent(root, space='  ')
    ET.ElementTree(root).write(output / 'sitemap.xml', encoding='utf-8', xml_declaration=True)


def render(source, output, latest, releases):
    if not stable(latest):
        raise ValueError('Latest release is not stable')
    history = sorted((r for r in releases if stable(r)), key=lambda r: r['published_at'], reverse=True)
    if not any(r['tag_name'] == latest['tag_name'] for r in history):
        raise ValueError('Latest release missing from release history')
    if output.exists():
        raise ValueError('Output directory must not already exist')
    shutil.copytree(source, output)
    pages = set(output.rglob('*.html'))
    expected = {output / folder / name for folder in ('', 'fr') for name in SEO}
    if pages != expected:
        raise ValueError('Website pages and translated SEO entries do not match')
    for page in pages:
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
        page.write_text(search_metadata(html, page.name, fr), encoding='utf-8')
    write_sitemap(output)
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
