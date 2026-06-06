(function () {
    const flat = window.__categoriesFlat || [];
    const selectedId = window.__selectedCategoryId;
    const l1 = document.getElementById('catLevel1');
    const l2 = document.getElementById('catLevel2');
    const l3 = document.getElementById('catLevel3');
    const hidden = document.getElementById('finalCategoryId');
    const preview = document.getElementById('categoryPathPreview');
    if (!l1 || !hidden) return;

    const childrenOf = (parentId) =>
        flat.filter(c => (c.ParentId ?? null) === (parentId ?? null));

    const fill = (sel, items, placeholder) => {
        sel.innerHTML = `<option value="">${placeholder}</option>`;
        items.forEach(c => {
            const opt = document.createElement('option');
            opt.value = c.Id;
            opt.textContent = c.Name;
            sel.appendChild(opt);
        });
        sel.disabled = items.length === 0;
    };

    const updatePreview = () => {
        const id = parseInt(hidden.value, 10);
        const cat = flat.find(c => c.Id === id);
        preview.textContent = cat ? cat.FullPath : '';
    };

    const setFinal = (id) => {
        hidden.value = id || '';
        updatePreview();
    };

    l1.addEventListener('change', () => {
        const id = l1.value ? parseInt(l1.value, 10) : null;
        fill(l2, id ? childrenOf(id) : [], '— Select Sub Category —');
        fill(l3, [], '— Select Child Category —');
        setFinal(id);
    });

    l2.addEventListener('change', () => {
        fill(l3, l2.value ? childrenOf(parseInt(l2.value, 10)) : [], '— Select Child Category —');
        setFinal(l2.value ? parseInt(l2.value, 10) : (l1.value ? parseInt(l1.value, 10) : null));
    });

    l3.addEventListener('change', () => {
        const id = l3.value ? parseInt(l3.value, 10)
            : l2.value ? parseInt(l2.value, 10)
            : l1.value ? parseInt(l1.value, 10) : null;
        setFinal(id);
    });

    if (selectedId) {
        const chain = [];
        let cur = flat.find(c => c.Id === selectedId);
        while (cur) {
            chain.unshift(cur);
            cur = cur.ParentId ? flat.find(c => c.Id === cur.ParentId) : null;
        }
        if (chain[0]) { l1.value = chain[0].Id; fill(l2, childrenOf(chain[0].Id), '— Select Sub Category —'); }
        if (chain[1]) { l2.value = chain[1].Id; fill(l3, childrenOf(chain[1].Id), '— Select Child Category —'); }
        if (chain[2]) l3.value = chain[2].Id;
        setFinal(selectedId);
    }
})();
