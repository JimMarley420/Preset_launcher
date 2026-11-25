'use strict';
let theme;
try { theme = localStorage.getItem('pl-theme'); } catch {}
function setTheme(value) {
  document.documentElement.dataset.theme = value;
  try { localStorage.setItem('pl-theme', value); } catch {}
}
setTheme(theme === 'dark' || theme === 'light' ? theme : (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'));
document.querySelector('.theme')?.addEventListener('click', () => setTheme(document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark'));
for (const img of document.querySelectorAll('img[data-screenshot]')) {
  const fallback = () => { img.hidden = true; img.nextElementSibling.hidden = false; };
  img.addEventListener('error', fallback);
  if (img.complete && !img.naturalWidth) fallback();
}
const presets = {
  work: ['Visual Studio Code', 'Windows Terminal', 'GitHub Desktop'],
  gaming: ['Steam', 'Discord', 'OBS Studio'],
  office: ['Outlook', 'Microsoft Teams', 'Firefox'],
};
const list = document.querySelector('#example-apps');
const buttons = [...document.querySelectorAll('[data-preset]')];
if (list) {
  for (const btn of buttons) {
    btn.setAttribute('aria-pressed', String(btn.classList.contains('selected')));
    btn.addEventListener('click', () => {
      for (const other of buttons) {
        const selected = other === btn;
        other.classList.toggle('selected', selected);
        other.setAttribute('aria-pressed', String(selected));
      }
      list.replaceChildren(...presets[btn.dataset.preset].map(name => {
        const li = document.createElement('li');
        li.textContent = name;
        return li;
      }));
    });
  }
}
