import tempfile
import unittest
from pathlib import Path
from html.parser import HTMLParser
from urllib.parse import urlsplit, unquote
from build_website import REPO, SOURCE, render, fetch_releases, release_downloads
from deploy_website import sync_output


def fixture(tag, suffixes=('', '_x86', '_arm64'), draft=False, prerelease=False):
    names = [f'PresetLauncher_Setup_{tag.removeprefix("v")}{suffix}.exe' for suffix in suffixes]
    names.append('SHA256SUMS.txt')
    return {'tag_name': tag, 'draft': draft, 'prerelease': prerelease,
            'published_at': '2026-10-01T10:00:00Z',
            'body': '<!-- presetlauncher:signature=secret -->\n## Changes\n<script>bad()</script>\n* Fixed & verified',
            'assets': [{'name': name, 'size': 1234567, 'browser_download_url': f'{REPO}/releases/download/{tag}/{name}'} for name in names]}


class Links(HTMLParser):
    def __init__(self):
        super().__init__()
        self.links = []
        self.launch = []

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if attrs.get('id') == 'demo-launch':
            self.launch.append((tag, attrs))
        for key in ('href', 'src'):
            if key in attrs:
                self.links.append(attrs[key])


class WebsiteTests(unittest.TestCase):
    def test_build_uses_release_data_on_all_pages(self):
        latest = fixture('v7.2.3')
        old = fixture('v7.2.2', ('',))
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / 'site'
            render(SOURCE, output, latest, [latest, old, fixture('v9.0.0', draft=True), fixture('v8.0.0', prerelease=True)])
            pages = list(output.rglob('*.html'))
            self.assertEqual(len(pages), 18)
            self.assertTrue((output / '.nojekyll').is_file())
            for page in pages:
                with self.subTest(page=page.relative_to(output)):
                    body = page.read_text()
                    self.assertIn('PresetLauncher 7.2.3 ', body)
                    self.assertNotIn('1.5.0', body)
                    self.assertNotIn('<!-- RELEASE_', body)
                    self.assertNotIn('<figcaption>', body)
                    links = Links(); links.feed(body)
                    for href in links.links:
                        url = urlsplit(href)
                        if not url.scheme and not url.netloc and url.path:
                            self.assertTrue((page.parent / unquote(url.path)).is_file(), href)
                    if page.name == 'index.html':
                        self.assertEqual(len(links.launch), 1)
                        self.assertEqual(links.launch[0][0], 'button')
                        self.assertIn('disabled', links.launch[0][1])
                        self.assertNotIn('Simulate launch', body)
                        self.assertNotIn('demo-status', body)
                    elif page.name == 'download.html':
                        for asset in latest['assets']:
                            self.assertIn(asset['browser_download_url'], body)
                        self.assertIn('1,2 Mo' if page.parent.name == 'fr' else '1.2 MB', body)
                    elif page.name == 'changelog.html':
                        self.assertIn('v7.2.2', body)
                        self.assertNotIn('v9.0.0', body)
                        self.assertNotIn('v8.0.0', body)
                        self.assertNotIn('signature=secret', body)
                        self.assertNotIn('<script>bad()', body)
                        self.assertIn('&lt;script&gt;bad()', body)

    def test_only_available_architectures_linked(self):
        text = release_downloads(fixture('v7.2.3', ('',)), False)
        self.assertIn('Windows x64', text)
        self.assertNotIn('Windows x86', text)
        self.assertNotIn('Windows ARM64', text)

    def test_reject_untrusted_asset_url(self):
        release = fixture('v7.2.3')
        release['assets'][0]['browser_download_url'] = 'https://example.org/installer.exe'
        with self.assertRaises(ValueError):
            release_downloads(release, False)

    def test_deploy_sync_preserves_git_metadata_and_removes_old_pages(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            output = root / 'site'
            output.mkdir()
            (output / 'index.html').write_text('new')
            (output / '.nojekyll').touch()
            checkout = root / 'pages'
            checkout.mkdir()
            (checkout / '.git').mkdir()
            (checkout / '.git' / 'HEAD').write_text('ref: refs/heads/gh-pages')
            (checkout / 'old.html').write_text('old')
            sync_output(output, checkout)
            self.assertEqual((checkout / 'index.html').read_text(), 'new')
            self.assertTrue((checkout / '.nojekyll').is_file())
            self.assertFalse((checkout / 'old.html').exists())
            self.assertEqual((checkout / '.git' / 'HEAD').read_text(), 'ref: refs/heads/gh-pages')

    def test_pagination(self):
        calls = []
        latest = fixture('v7.2.3')
        def request(url, token):
            calls.append(url)
            return latest if url.endswith('/latest') else [latest] * 100 if url.endswith('page=1') else [latest]
        actual, history = fetch_releases(request, 'https://api.github.com/repos/example', 'test')
        self.assertEqual(actual, latest)
        self.assertEqual(len(history), 101)
        self.assertEqual(len(calls), 3)


if __name__ == '__main__':
    unittest.main()
