using System.Collections.Generic;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EnhancedOrders
{
    // Order panels built into the PDA Orders panel, in the empty space below the job filter
    // checkboxes (pnlJobs: types 0.8-0.95, filters 0.55-0.75, nothing below in order mode).
    // One panel per OrderPicker (Uninstall, Repair, Haul); only the active order's panel is shown.
    //
    // Checkboxes are clones of the game's own filter checkbox: a square whose Background
    // fills it, sized by pnlToggles' layout, with the label 50px below. Clones are kept
    // square with the label moved to the right. The game wires the originals up with
    // AddListener at runtime, so clones come without its filter logic. The on/off look
    // is GUIToggleSwap swapping the Background sprite; SetState applies that directly.
    internal static class PdaPanel
    {
        internal static bool Failed;
        private static GUIPDA _builtFor;
        private static readonly List<Panel> _panels = new List<Panel>();

        private static Toggle _styleToggle;
        private static TMP_Text _styleText;
        private static float _rowHeight;
        private static float _boxSize;
        private static float _fontSize;

        // Called every frame from Plugin.Update.
        internal static void Tick()
        {
            if (Failed) return;
            GUIPDA pda = GUIPDA.instance;
            if (pda == null) return;

            if (_builtFor != pda || _panels.Count == 0 || _panels.Exists(p => p.Root == null))
            {
                if (!Build(pda)) return;
            }

            OrderPicker active = OrderPicker.Active;
            foreach (Panel p in _panels) p.Tick(active == p.Picker);

            // The game won't start a drag-select while any UI element is selected, so
            // release our controls as soon as the mouse button is.
            EventSystem es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null && !Input.GetMouseButton(0)
                && _panels.Exists(p => es.currentSelectedGameObject.transform.IsChildOf(p.Root.transform)))
                es.SetSelectedGameObject(null);
        }

        private static bool Build(GUIPDA pda)
        {
            foreach (Panel p in _panels)
                if (p.Root != null) Object.Destroy(p.Root);
            _panels.Clear();
            try
            {
                Transform jobs = pda.transform.Find("pnlJobs");
                Transform filters = jobs?.Find("pnlJobFilters");
                _styleText = filters?.Find("lblFilter")?.GetComponent<TMP_Text>();
                _styleToggle = filters?.Find("pnlToggles/chkWalls")?.GetComponent<Toggle>();
                if (jobs == null || _styleText == null || _styleToggle == null)
                {
                    Plugin.Log.LogWarning("PDA Orders panel not found; using the on-screen window instead.");
                    Failed = true;
                    return false;
                }
                _fontSize = Plugin.FontSize.Value;
                _boxSize = Mathf.Round(_fontSize * 0.9f);
                _rowHeight = Mathf.Round(_fontSize * 1.5f);

                _panels.Add(new Panel(jobs, OrderPicker.Uninstall, null));
                _panels.Add(new Panel(jobs, OrderPicker.Repair, new[]
                {
                    new SliderSpec(Plugin.MaxCondition, "Repair only below {0}%"),
                    new SliderSpec(Plugin.StopRestoreAt, "Stop restoring at {0}%"),
                }));
                _panels.Add(new Panel(jobs, OrderPicker.Haul, null));

                _builtFor = pda;
                Plugin.Log.LogInfo("Order panels added to the PDA Orders panel.");
                return true;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError("Couldn't build the PDA panels, using the on-screen window instead: " + e);
                foreach (Panel p in _panels)
                    if (p.Root != null) Object.Destroy(p.Root);
                _panels.Clear();
                Failed = true;
                return false;
            }
        }

        // Button only reacts to the left mouse button; this adds a right-click action.
        private class RightClick : MonoBehaviour, IPointerClickHandler
        {
            public System.Action Action;

            public void OnPointerClick(PointerEventData e)
            {
                if (e.button == PointerEventData.InputButton.Right) Action?.Invoke();
            }
        }

        private class SliderSpec
        {
            public readonly ConfigEntry<int> Entry;
            public readonly string Format;
            public TMP_Text Label;
            public Slider Slider;

            public SliderSpec(ConfigEntry<int> entry, string format)
            {
                Entry = entry;
                Format = format;
            }

            public void Sync()
            {
                if (Mathf.RoundToInt(Slider.value) != Entry.Value) Slider.SetValueWithoutNotify(Entry.Value);
                Label.text = string.Format(Format, Entry.Value);
            }
        }

        private class Panel
        {
            public readonly OrderPicker Picker;
            public readonly GameObject Root;
            private readonly SliderSpec[] _sliders;
            private readonly TMP_Text _status;
            private readonly Toggle _pick;
            private readonly GameObject _buttons;
            private readonly Button _confirm;
            private readonly TMP_Text _confirmLabel;
            private readonly RectTransform _list;
            private readonly List<KeyValuePair<string, Toggle>> _rows = new List<KeyValuePair<string, Toggle>>();
            private readonly List<KeyValuePair<string, Toggle>> _groupRows = new List<KeyValuePair<string, Toggle>>();
            private readonly Button[] _presetButtons = new Button[OrderPicker.PresetSlots];
            private readonly TMP_Text[] _presetLabels = new TMP_Text[OrderPicker.PresetSlots];
            private int _builtVersion = -1;

            public Panel(Transform jobs, OrderPicker picker, SliderSpec[] sliders)
            {
                Picker = picker;
                _sliders = sliders ?? new SliderSpec[0];

                Root = new GameObject("pnlEnhancedOrders" + picker.Order, typeof(RectTransform));
                RectTransform rt = (RectTransform)Root.transform;
                rt.SetParent(jobs, false);
                // Above pnlNavigation (bottom ~6%) and below the filter labels, which hang
                // under the checkboxes to ~0.545.
                rt.anchorMin = new Vector2(0f, 0.075f);
                rt.anchorMax = new Vector2(1f, 0.53f);
                rt.offsetMin = new Vector2(10f, 0f);
                rt.offsetMax = new Vector2(-10f, -4f);
                rt.SetAsLastSibling();

                // Invisible backing so clicks between the controls stay in the PDA.
                Image back = Root.AddComponent<Image>();
                back.color = new Color(0f, 0f, 0f, 0f);

                float y = 0f;
                _status = MakeLabel(rt, ref y);
                foreach (SliderSpec s in _sliders) MakeSlider(rt, s, ref y);
                // Pick toggle and preset slots share a row. Click an empty slot to save the
                // ticks, a filled one to load it; right-click clears a slot.
                float pickTop = y;
                _pick = MakeToggle(rt, "Pick area", ref y, 0f, 0.36f);
                _pick.onValueChanged.AddListener(picker.SetPicking);
                RectTransform presets = NewRow("pnlPresets", rt, ref pickTop);
                for (int i = 0; i < OrderPicker.PresetSlots; i++)
                {
                    int slot = i;
                    float w = 0.64f / OrderPicker.PresetSlots;
                    Button b = MakeButton(presets, 0.36f + w * i + 0.01f, 0.36f + w * (i + 1) - 0.01f, () =>
                    {
                        if (picker.PresetName(slot) == null) picker.SavePreset(slot);
                        else picker.LoadPreset(slot);
                    }, out _presetLabels[i]);
                    RectTransform br = (RectTransform)b.transform;
                    br.offsetMin = new Vector2(0f, 3f);
                    br.offsetMax = new Vector2(0f, -3f);
                    b.gameObject.AddComponent<RightClick>().Action = () => picker.ClearPreset(slot);
                    _presetButtons[i] = b;
                }

                // Confirm / Cancel sit at the bottom; the scrolling type list fills the space between.
                float btnHeight = Mathf.Round(_rowHeight * 1.3f);
                RectTransform buttons = NewRect("pnlConfirm", rt, Vector2.zero, new Vector2(1f, 0f));
                buttons.pivot = new Vector2(0.5f, 0f);
                buttons.sizeDelta = new Vector2(0f, btnHeight);
                _buttons = buttons.gameObject;
                _confirm = MakeButton(buttons, 0f, 0.68f, picker.Confirm, out _confirmLabel);
                MakeButton(buttons, 0.72f, 1f, () => picker.SetPicking(false), out TMP_Text cancel);
                cancel.text = "CANCEL";

                _list = MakeScroll(rt, y, btnHeight + 6f);
                Root.SetActive(false);
            }

            public void Tick(bool show)
            {
                if (Root.activeSelf != show) Root.SetActive(show);
                if (!show) return;

                if (_builtVersion != Picker.Version) RebuildList();

                _status.text = Picker.Status;
                foreach (SliderSpec s in _sliders) s.Sync();
                SetState(_pick, Picker.Picking);
                bool hasPicks = Picker.Candidates.Count > 0;
                if (_buttons.activeSelf != hasPicks) _buttons.SetActive(hasPicks);
                _confirm.interactable = Picker.SelectedCount > 0;
                _confirmLabel.text = Picker.ConfirmText;
                for (int i = 0; i < OrderPicker.PresetSlots; i++)
                {
                    string name = Picker.PresetName(i);
                    _presetLabels[i].text = name ?? "+ SAVE";
                    _presetButtons[i].interactable = name != null || Picker.SelectedCount > 0;
                }
                foreach (var row in _rows)
                    SetState(row.Value, Picker.Selected.Contains(row.Key));
                foreach (var row in _groupRows)
                    SetState(row.Value, Picker.GroupTicked(row.Key));
            }

            private void RebuildList()
            {
                foreach (Transform child in _list) Object.Destroy(child.gameObject);
                _rows.Clear();
                _groupRows.Clear();

                float y = 0f;
                string group = null;
                foreach (OrderPicker.Entry e in Picker.Candidates)
                {
                    if (e.Group != group)
                    {
                        string g = group = e.Group;
                        Toggle h = MakeToggle(_list, g, ref y);
                        SetState(h, Picker.GroupTicked(g));
                        h.onValueChanged.AddListener(on => Picker.ToggleGroup(g, on));
                        _groupRows.Add(new KeyValuePair<string, Toggle>(g, h));
                    }
                    string name = e.Name;
                    Toggle t = MakeToggle(_list, $"{e.Label}  x{e.Count}", ref y, _boxSize + 8f);
                    SetState(t, Picker.Selected.Contains(name));
                    t.onValueChanged.AddListener(on => Picker.Toggle(name, on));
                    _rows.Add(new KeyValuePair<string, Toggle>(name, t));
                }
                _list.sizeDelta = new Vector2(0f, -y);
                _list.anchoredPosition = Vector2.zero;
                _builtVersion = Picker.Version;
            }
        }

        // Rows are stacked top-down by hand; y is the running offset from the top (negative).
        private static TMP_Text MakeLabel(RectTransform parent, ref float y)
        {
            RectTransform lblRt = NewRow("lbl", parent, ref y);
            TextMeshProUGUI lbl = lblRt.gameObject.AddComponent<TextMeshProUGUI>();
            lbl.font = _styleText.font;
            lbl.fontSharedMaterial = _styleText.fontSharedMaterial;
            lbl.fontStyle = _styleText.fontStyle;
            lbl.color = _styleText.color;
            lbl.enableAutoSizing = false;
            lbl.fontSize = _fontSize;
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            lbl.textWrappingMode = TextWrappingModes.NoWrap;
            lbl.overflowMode = TextOverflowModes.Ellipsis;
            lbl.raycastTarget = false;
            return lbl;
        }

        // One row: the cloned checkbox as a square on the left, our own label to its right.
        // Clicking the label toggles too.
        // indent shifts the row right; maxX limits the label to part of the width.
        private static Toggle MakeToggle(RectTransform parent, string text, ref float y,
            float indent = 0f, float maxX = 1f)
        {
            float top = y;
            TMP_Text label = MakeLabel(parent, ref y);
            label.rectTransform.anchorMax = new Vector2(maxX, 1f);
            label.margin = new Vector4(indent + _boxSize + 8f, 0f, 0f, 0f);
            label.text = text;
            label.raycastTarget = true;

            Toggle t = Object.Instantiate(_styleToggle, parent, false);
            t.name = "chkPick";
            t.onValueChanged = new Toggle.ToggleEvent();
            t.group = null;
            t.navigation = new Navigation { mode = Navigation.Mode.None };
            SetState(t, false);
            foreach (TMP_Text old in t.GetComponentsInChildren<TMP_Text>(true))
                if (old.transform != t.transform) Object.Destroy(old.gameObject);

            RectTransform rt = (RectTransform)t.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(_boxSize, _boxSize);
            rt.anchoredPosition = new Vector2(indent + 2f, top - _rowHeight / 2f);

            Button click = label.gameObject.AddComponent<Button>();
            click.transition = Selectable.Transition.None;
            click.navigation = new Navigation { mode = Navigation.Mode.None };
            click.onClick.AddListener(() => t.isOn = !t.isOn);
            return t;
        }

        // One row: label on the left, 1-100 slider on the right.
        private static void MakeSlider(RectTransform parent, SliderSpec spec, ref float y)
        {
            float top = y;
            spec.Label = MakeLabel(parent, ref y);
            RectTransform lr = spec.Label.rectTransform;
            lr.anchorMax = new Vector2(0.6f, 1f);

            Color accent = _styleText.color;
            RectTransform sRt = NewRect("slider", parent, new Vector2(0.62f, 1f), new Vector2(1f, 1f));
            sRt.pivot = new Vector2(0.5f, 1f);
            sRt.anchoredPosition = new Vector2(0f, top);
            sRt.sizeDelta = new Vector2(-6f, _rowHeight);

            RectTransform trackRt = NewRect("Background", sRt, new Vector2(0f, 0.4f), new Vector2(1f, 0.6f));
            Image track = trackRt.gameObject.AddComponent<Image>();
            track.color = new Color(accent.r, accent.g, accent.b, 0.25f);

            RectTransform fillArea = NewRect("Fill Area", sRt, new Vector2(0f, 0.4f), new Vector2(1f, 0.6f));
            RectTransform fillRt = NewRect("Fill", fillArea, Vector2.zero, new Vector2(0f, 1f));
            Image fill = fillRt.gameObject.AddComponent<Image>();
            fill.color = new Color(accent.r, accent.g, accent.b, 0.6f);
            fill.raycastTarget = false;

            RectTransform handleArea = NewRect("Handle Slide Area", sRt, new Vector2(0f, 0.15f), new Vector2(1f, 0.85f));
            handleArea.offsetMin = new Vector2(4f, 0f);
            handleArea.offsetMax = new Vector2(-4f, 0f);
            RectTransform handleRt = NewRect("Handle", handleArea, Vector2.zero, new Vector2(0f, 1f));
            handleRt.sizeDelta = new Vector2(8f, 0f);
            Image handle = handleRt.gameObject.AddComponent<Image>();
            handle.color = accent;

            Slider slider = sRt.gameObject.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 1f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.SetValueWithoutNotify(spec.Entry.Value);
            spec.Slider = slider;
            slider.onValueChanged.AddListener(v =>
            {
                spec.Entry.Value = Mathf.RoundToInt(v);
                spec.Sync();
            });
            spec.Sync();
        }

        // Framed button in the PDA text colour, spanning [xMin, xMax] of the parent's width.
        private static Button MakeButton(RectTransform parent, float xMin, float xMax,
            UnityEngine.Events.UnityAction onClick, out TMP_Text label)
        {
            Color accent = _styleText.color;
            RectTransform rt = NewRect("btn", parent, new Vector2(xMin, 0f), new Vector2(xMax, 1f));
            Image bg = rt.gameObject.AddComponent<Image>();
            bg.color = new Color(accent.r, accent.g, accent.b, 0.18f);
            Outline frame = rt.gameObject.AddComponent<Outline>();
            frame.effectColor = accent;
            frame.effectDistance = new Vector2(1f, -1f);

            float unused = 0f;
            label = MakeLabel(rt, ref unused);
            RectTransform lr = label.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;
            label.alignment = TextAlignmentOptions.Center;

            Button b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock c = b.colors;
            c.highlightedColor = new Color(1.6f, 1.6f, 1.6f, 1.6f);
            c.pressedColor = new Color(2.2f, 2.2f, 2.2f, 2.2f);
            c.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            c.colorMultiplier = 1f;
            b.colors = c;
            b.onClick.AddListener(onClick);
            return b;
        }

        private static void SetState(Toggle t, bool on)
        {
            if (t.isOn != on) t.SetIsOnWithoutNotify(on);
            var swap = t.GetComponent<GUIToggleSwap>();
            // The overrideSprite getter returns the active sprite, so just assign; the
            // setter ignores unchanged values.
            if (swap != null && t.targetGraphic is Image img)
                img.overrideSprite = on ? swap.selectedSprite : null;
        }

        // Scroll area from y (offset from the top) down to bottomPad above the bottom.
        private static RectTransform MakeScroll(RectTransform parent, float y, float bottomPad)
        {
            RectTransform view = NewRect("scrollPicks", parent, Vector2.zero, Vector2.one);
            view.offsetMin = new Vector2(0f, bottomPad);
            view.offsetMax = new Vector2(0f, y - 2f);
            Image hit = view.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            view.gameObject.AddComponent<RectMask2D>();

            RectTransform content = NewRect("content", view, new Vector2(0f, 1f), new Vector2(1f, 1f));
            content.pivot = new Vector2(0.5f, 1f);

            ScrollRect sr = view.gameObject.AddComponent<ScrollRect>();
            sr.viewport = view;
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = _rowHeight;
            return content;
        }

        private static RectTransform NewRow(string name, RectTransform parent, ref float y)
        {
            RectTransform rt = NewRect(name, parent, new Vector2(0f, 1f), new Vector2(1f, 1f));
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(0f, _rowHeight);
            y -= _rowHeight;
            return rt;
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }
    }
}
