(() => {
  const tabs = Array.from(document.querySelectorAll('.profile-tabs [role="tab"]'));
  if (!tabs.length || !window.bootstrap) return;
  const update = active => tabs.forEach(tab => {
    const selected = tab === active;
    tab.tabIndex = selected ? 0 : -1;
    tab.setAttribute('aria-selected', String(selected));
  });
  tabs.forEach((tab, index) => {
    tab.addEventListener('shown.bs.tab', () => {
      update(tab);
      history.replaceState(null, '', tab.dataset.bsTarget);
    });
    tab.addEventListener('keydown', event => {
      let target;
      if (event.key === 'ArrowRight') target = tabs[(index + 1) % tabs.length];
      if (event.key === 'ArrowLeft') target = tabs[(index - 1 + tabs.length) % tabs.length];
      if (event.key === 'Home') target = tabs[0];
      if (event.key === 'End') target = tabs[tabs.length - 1];
      if (!target) return;
      event.preventDefault();
      bootstrap.Tab.getOrCreateInstance(target).show();
      target.focus();
    });
  });
  const requested = tabs.find(tab => tab.dataset.bsTarget === location.hash);
  if (requested) bootstrap.Tab.getOrCreateInstance(requested).show();
  update(requested || tabs.find(tab => tab.classList.contains('active')));
})();
