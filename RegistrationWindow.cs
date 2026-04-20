using System.Collections.Generic;
using Parkitect.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NaturalPeepMovement
{
    public class RegistrationWindow : UIWindow
    {
        public const string UniqueTag = "NaturalPeepMovement.RegistrationWindow";

        private static readonly Color PanelColor = HexColor("E9EAF2");
        private static readonly Color TextColor = HexColor("585D5F");
        private static readonly Color RegisterColor = new Color(0.25f, 0.45f, 0.70f, 1f);
        private static readonly Color UnregisterColor = new Color(0.70f, 0.30f, 0.30f, 1f);
        private static readonly Color ListBackgroundColor = HexColor("D7D7E8");
        private static readonly Color RowBackgroundColor = HexColor("C1C2D3");
        private static readonly Color InputBackgroundColor = HexColor("F5F6F9");
        private static readonly Color PlaceholderColor = HexColor("9098A0");
        private static readonly Color StatusSuccessColor = HexColor("3F8B43");
        private static readonly Color StatusFailureColor = HexColor("B23A48");

        private const int InnerPanelCornerRadius = 10;
        private const int SubPanelCornerRadius = 5;

        private static readonly Dictionary<int, Sprite> _roundedSpriteCache = new Dictionary<int, Sprite>();

        private const string DropdownPlaceholder = "Existing presets…";

        private TextMeshProUGUI _statusText;
        private TextMeshProUGUI _buttonLabel;
        private Image _buttonImage;
        private RectTransform _listContent;
        private TMP_InputField _inputField;
        private TextMeshProUGUI _statusLineText;
        private Button _loadButton;
        private Button _saveButton;
        private TMP_Dropdown _presetDropdown;
        private string _decoName;

        public string DecoName => _decoName;

        public void SetDeco(string decoName)
        {
            _decoName = decoName;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (_statusText == null) return;
            bool registered = MarkerRegistry.Contains(_decoName);
            _statusText.text =
                "<b>" + _decoName + "</b>\n" +
                (registered ? "Currently REGISTERED" : "Currently NOT registered");

            if (_buttonLabel != null)
                _buttonLabel.text = registered ? "Unregister" : "Register";
            if (_buttonImage != null)
                _buttonImage.color = registered ? UnregisterColor : RegisterColor;
        }

        private void OnButtonClicked()
        {
            if (string.IsNullOrEmpty(_decoName)) return;
            if (MarkerRegistry.Contains(_decoName))
                MarkerRegistry.Unregister(_decoName);
            else
                MarkerRegistry.Register(_decoName);
            UpdateStatus();
            RebuildList();
        }

        private void OnRowUnregisterClicked(string name)
        {
            MarkerRegistry.Unregister(name);
            UpdateStatus();
            RebuildList();
        }

        private void OnInputChanged(string value)
        {
            bool hasText = !string.IsNullOrWhiteSpace(value);
            if (_loadButton != null) _loadButton.interactable = hasText;
            if (_saveButton != null) _saveButton.interactable = hasText;
        }

        private void OnLoadClicked()
        {
            if (_inputField == null) return;
            string raw = _inputField.text;
            string error;
            int loadedCount;
            if (MarkerRegistry.LoadFromFile(raw, out error, out loadedCount))
            {
                SetStatusLine("Loaded " + loadedCount + " markers from " + raw + ".json", true);
                UpdateStatus();
                RebuildList();
            }
            else
            {
                SetStatusLine(error, false);
            }
        }

        private void OnSaveClicked()
        {
            if (_inputField == null) return;
            string raw = _inputField.text;
            string error;
            int savedCount;
            if (MarkerRegistry.SaveToFile(raw, out error, out savedCount))
            {
                SetStatusLine("Saved " + savedCount + " markers to " + raw + ".json", true);
                RefreshPresetDropdown();
            }
            else
            {
                SetStatusLine(error, false);
            }
        }

        private void OnPresetDropdownChanged(int index)
        {
            if (_presetDropdown == null || _inputField == null) return;
            if (index <= 0 || index >= _presetDropdown.options.Count) return;

            string selected = _presetDropdown.options[index].text;
            _inputField.text = selected;

            // Snap dropdown back to the placeholder so future picks of the same item still fire.
            _presetDropdown.SetValueWithoutNotify(0);
            if (_presetDropdown.captionText != null)
                _presetDropdown.captionText.text = DropdownPlaceholder;
        }

        private void RefreshPresetDropdown()
        {
            if (_presetDropdown == null) return;
            _presetDropdown.options.Clear();
            _presetDropdown.options.Add(new TMP_Dropdown.OptionData(DropdownPlaceholder));

            List<string> presets = MarkerRegistry.ListPresets();
            for (int i = 0; i < presets.Count; i++)
                _presetDropdown.options.Add(new TMP_Dropdown.OptionData(presets[i]));

            _presetDropdown.SetValueWithoutNotify(0);
            if (_presetDropdown.captionText != null)
                _presetDropdown.captionText.text = DropdownPlaceholder;
        }

        private void SetStatusLine(string message, bool success)
        {
            if (_statusLineText == null) return;
            _statusLineText.text = message ?? string.Empty;
            _statusLineText.color = success ? StatusSuccessColor : StatusFailureColor;
        }

        private void RebuildList()
        {
            if (_listContent == null) return;

            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Object.Destroy(_listContent.GetChild(i).gameObject);

            List<string> names = MarkerRegistry.GetAll();
            for (int i = 0; i < names.Count; i++)
                AddRow(names[i]);
        }

        // Refresh everything (used after external registry changes).
        public void RefreshAll()
        {
            UpdateStatus();
            RebuildList();
            RefreshPresetDropdown();
        }

        private void AddRow(string name)
        {
            // Anchors keep button width fixed regardless of name length.
            const float ButtonWidth = 84f;
            const float ButtonHeight = 22f;
            const float HPad = 6f;
            const float Gap = 6f;
            const float VPad = 2f;

            GameObject rowGO = new GameObject("Row_" + name, typeof(RectTransform));
            rowGO.transform.SetParent(_listContent, false);

            Image rowBG = rowGO.AddComponent<Image>();
            rowBG.sprite = GetRoundedSprite(SubPanelCornerRadius);
            rowBG.type = Image.Type.Sliced;
            rowBG.color = RowBackgroundColor;

            LayoutElement rowLE = rowGO.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 26f;
            rowLE.flexibleWidth = 1f;

            GameObject xGO = new GameObject("Unregister", typeof(RectTransform));
            xGO.transform.SetParent(rowGO.transform, false);
            Image xImg = xGO.AddComponent<Image>();
            xImg.sprite = GetRoundedSprite(SubPanelCornerRadius);
            xImg.type = Image.Type.Sliced;
            xImg.color = UnregisterColor;
            Button xBtn = xGO.AddComponent<Button>();
            xBtn.targetGraphic = xImg;

            RectTransform xRT = (RectTransform)xGO.transform;
            xRT.anchorMin = new Vector2(1f, 0.5f);
            xRT.anchorMax = new Vector2(1f, 0.5f);
            xRT.pivot = new Vector2(1f, 0.5f);
            xRT.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            xRT.anchoredPosition = new Vector2(-HPad, 0f);

            GameObject nameGO = new GameObject("Name", typeof(RectTransform));
            nameGO.transform.SetParent(rowGO.transform, false);
            TextMeshProUGUI nameText = nameGO.AddComponent<TextMeshProUGUI>();
            nameText.text = name;
            nameText.fontSize = 12f;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            nameText.color = TextColor;
            nameText.enableWordWrapping = false;
            nameText.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform nameRT = (RectTransform)nameGO.transform;
            nameRT.anchorMin = new Vector2(0f, 0f);
            nameRT.anchorMax = new Vector2(1f, 1f);
            nameRT.pivot = new Vector2(0f, 0.5f);
            nameRT.offsetMin = new Vector2(HPad, VPad);
            nameRT.offsetMax = new Vector2(-(HPad + ButtonWidth + Gap), -VPad);

            GameObject xLabelGO = new GameObject("Label", typeof(RectTransform));
            xLabelGO.transform.SetParent(xGO.transform, false);
            RectTransform xLabelRT = (RectTransform)xLabelGO.transform;
            xLabelRT.anchorMin = Vector2.zero;
            xLabelRT.anchorMax = Vector2.one;
            xLabelRT.offsetMin = Vector2.zero;
            xLabelRT.offsetMax = Vector2.zero;
            TextMeshProUGUI xLabel = xLabelGO.AddComponent<TextMeshProUGUI>();
            xLabel.text = "Unregister";
            xLabel.fontSize = 11f;
            xLabel.alignment = TextAlignmentOptions.Center;
            xLabel.color = Color.white;

            string captured = name;
            xBtn.onClick.AddListener(() => OnRowUnregisterClicked(captured));
        }

        private static RectTransform BuildScrollView(GameObject parent)
        {
            GameObject scrollGO = new GameObject("ListScroll", typeof(RectTransform));
            scrollGO.transform.SetParent(parent.transform, false);

            Image scrollBG = scrollGO.AddComponent<Image>();
            scrollBG.sprite = GetRoundedSprite(SubPanelCornerRadius);
            scrollBG.type = Image.Type.Sliced;
            scrollBG.color = ListBackgroundColor;

            LayoutElement scrollLE = scrollGO.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;
            scrollLE.flexibleWidth = 1f;
            scrollLE.minHeight = 80f;

            ScrollRect scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 16f;

            GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            RectTransform viewportRT = (RectTransform)viewportGO.transform;
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;
            // Rounded mask clips scrolled content to panel shape.
            Image viewportImg = viewportGO.AddComponent<Image>();
            viewportImg.sprite = GetRoundedSprite(SubPanelCornerRadius);
            viewportImg.type = Image.Type.Sliced;
            viewportImg.color = new Color(1f, 1f, 1f, 0.01f);
            Mask viewportMask = viewportGO.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            GameObject contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            RectTransform contentRT = (RectTransform)contentGO.transform;
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentVLG = contentGO.AddComponent<VerticalLayoutGroup>();
            contentVLG.spacing = 2f;
            contentVLG.padding = new RectOffset(4, 4, 4, 4);
            contentVLG.childAlignment = TextAnchor.UpperLeft;
            contentVLG.childControlWidth = true;
            contentVLG.childControlHeight = true;
            contentVLG.childForceExpandWidth = true;
            contentVLG.childForceExpandHeight = false;

            ContentSizeFitter contentCSF = contentGO.AddComponent<ContentSizeFitter>();
            contentCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRT;
            scroll.content = contentRT;

            return contentRT;
        }

        public static RegistrationWindow Build(string decoName)
        {
            // Outer content GO; UIWindowFrame manages sizing.
            GameObject contentGO = new GameObject("MarkerRegistrationContent", typeof(RectTransform));
            RectTransform rt = (RectTransform)contentGO.transform;
            rt.sizeDelta = new Vector2(360f, 330f);

            UIWindowSettings settings = contentGO.AddComponent<UIWindowSettings>();
            settings.title = "Marker Registration";
            settings.uniqueTagString = UniqueTag;
            settings.contentDeterminesWindowSize = false;
            settings.destroyWhenClosed = true;
            settings.closable = true;
            settings.pinnable = false;
            settings.minimizable = false;
            settings.resizeability = UIWindowSettings.Resizeability.Vertical;
            settings.spawnLocation = UIWindowSettings.SpawnLocation.Free;
            settings.defaultWindowPosition = new Vector2(0.5f, 0.5f);

            // Inner rounded panel fills the frame area.
            GameObject inner = new GameObject("InnerPanel", typeof(RectTransform));
            inner.transform.SetParent(contentGO.transform, false);
            RectTransform innerRT = (RectTransform)inner.transform;
            innerRT.anchorMin = Vector2.zero;
            innerRT.anchorMax = Vector2.one;
            innerRT.offsetMin = Vector2.zero;
            innerRT.offsetMax = Vector2.zero;

            Image innerBG = inner.AddComponent<Image>();
            innerBG.sprite = GetRoundedSprite(InnerPanelCornerRadius);
            innerBG.type = Image.Type.Sliced;
            innerBG.color = PanelColor;

            VerticalLayoutGroup innerVLG = inner.AddComponent<VerticalLayoutGroup>();
            innerVLG.spacing = 8f;
            innerVLG.padding = new RectOffset(12, 12, 12, 12);
            innerVLG.childAlignment = TextAnchor.UpperCenter;
            innerVLG.childControlWidth = true;
            innerVLG.childControlHeight = true;
            innerVLG.childForceExpandWidth = true;
            innerVLG.childForceExpandHeight = false;

            GameObject statusGO = new GameObject("Status", typeof(RectTransform));
            statusGO.transform.SetParent(inner.transform, false);
            TextMeshProUGUI status = statusGO.AddComponent<TextMeshProUGUI>();
            status.text = "";
            status.alignment = TextAlignmentOptions.Center;
            status.fontSize = 14f;
            status.color = TextColor;
            status.enableWordWrapping = true;
            LayoutElement statusLE = statusGO.AddComponent<LayoutElement>();
            statusLE.preferredHeight = 48f;
            statusLE.flexibleWidth = 1f;

            GameObject btnGO = new GameObject("ToggleButton", typeof(RectTransform));
            btnGO.transform.SetParent(inner.transform, false);
            Image btnImg = btnGO.AddComponent<Image>();
            btnImg.sprite = GetRoundedSprite(SubPanelCornerRadius);
            btnImg.type = Image.Type.Sliced;
            btnImg.color = RegisterColor;
            Button btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            LayoutElement btnLE = btnGO.AddComponent<LayoutElement>();
            btnLE.preferredHeight = 36f;
            btnLE.flexibleWidth = 1f;

            GameObject btnLabelGO = new GameObject("Label", typeof(RectTransform));
            btnLabelGO.transform.SetParent(btnGO.transform, false);
            RectTransform btnLabelRT = (RectTransform)btnLabelGO.transform;
            btnLabelRT.anchorMin = Vector2.zero;
            btnLabelRT.anchorMax = Vector2.one;
            btnLabelRT.offsetMin = Vector2.zero;
            btnLabelRT.offsetMax = Vector2.zero;
            TextMeshProUGUI btnLabel = btnLabelGO.AddComponent<TextMeshProUGUI>();
            btnLabel.text = "Register";
            btnLabel.alignment = TextAlignmentOptions.Center;
            btnLabel.fontSize = 14f;
            btnLabel.color = Color.white;

            // Preset dropdown fills the input field below.
            TMP_Dropdown presetDropdown = BuildPresetDropdown(inner);

            // Input row + Load + Save (anchored for fixed widths).
            TMP_InputField inputField;
            Button loadButton;
            Button saveButton;
            BuildInputRow(inner, out inputField, out loadButton, out saveButton);

            // Status line; reserved height avoids layout shift.
            GameObject statusLineGO = new GameObject("StatusLine", typeof(RectTransform));
            statusLineGO.transform.SetParent(inner.transform, false);
            TextMeshProUGUI statusLine = statusLineGO.AddComponent<TextMeshProUGUI>();
            statusLine.text = "";
            statusLine.fontSize = 11f;
            statusLine.color = TextColor;
            statusLine.alignment = TextAlignmentOptions.MidlineLeft;
            statusLine.fontStyle = FontStyles.Italic;
            LayoutElement statusLineLE = statusLineGO.AddComponent<LayoutElement>();
            statusLineLE.preferredHeight = 16f;
            statusLineLE.flexibleWidth = 1f;

            GameObject headerGO = new GameObject("ListHeader", typeof(RectTransform));
            headerGO.transform.SetParent(inner.transform, false);
            TextMeshProUGUI header = headerGO.AddComponent<TextMeshProUGUI>();
            header.text = "<b>Registered Markers</b>";
            header.fontSize = 12f;
            header.alignment = TextAlignmentOptions.MidlineLeft;
            header.color = TextColor;
            LayoutElement headerLE = headerGO.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 18f;
            headerLE.flexibleWidth = 1f;

            RectTransform listContent = BuildScrollView(inner);

            RegistrationWindow win = contentGO.AddComponent<RegistrationWindow>();
            win._statusText = status;
            win._buttonLabel = btnLabel;
            win._buttonImage = btnImg;
            win._listContent = listContent;
            win._inputField = inputField;
            win._statusLineText = statusLine;
            win._loadButton = loadButton;
            win._saveButton = saveButton;
            win._presetDropdown = presetDropdown;
            win.SetDeco(decoName);
            win.RebuildList();
            win.RefreshPresetDropdown();

            btn.onClick.AddListener(win.OnButtonClicked);
            loadButton.onClick.AddListener(win.OnLoadClicked);
            saveButton.onClick.AddListener(win.OnSaveClicked);
            inputField.onValueChanged.AddListener(win.OnInputChanged);
            presetDropdown.onValueChanged.AddListener(win.OnPresetDropdownChanged);
            // Empty input → buttons disabled.
            win.OnInputChanged(inputField.text);

            return win;
        }

        private static TMP_Dropdown BuildPresetDropdown(GameObject parent)
        {
            const float DropdownHeight = 24f;
            const float PopupHeight = 140f;

            GameObject ddGO = new GameObject("PresetDropdown", typeof(RectTransform));
            ddGO.transform.SetParent(parent.transform, false);
            LayoutElement ddLE = ddGO.AddComponent<LayoutElement>();
            ddLE.preferredHeight = DropdownHeight;
            ddLE.flexibleWidth = 1f;

            Image bg = ddGO.AddComponent<Image>();
            bg.sprite = GetRoundedSprite(SubPanelCornerRadius);
            bg.type = Image.Type.Sliced;
            bg.color = InputBackgroundColor;

            // Caption: current selection, shown when popup is closed.
            GameObject labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(ddGO.transform, false);
            RectTransform labelRT = (RectTransform)labelGO.transform;
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = new Vector2(8f, 0f);
            labelRT.offsetMax = new Vector2(-24f, 0f);
            TextMeshProUGUI captionText = labelGO.AddComponent<TextMeshProUGUI>();
            captionText.text = DropdownPlaceholder;
            captionText.fontSize = 12f;
            captionText.color = TextColor;
            captionText.alignment = TextAlignmentOptions.MidlineLeft;
            captionText.enableWordWrapping = false;
            captionText.overflowMode = TextOverflowModes.Ellipsis;

            GameObject arrowGO = new GameObject("Arrow", typeof(RectTransform));
            arrowGO.transform.SetParent(ddGO.transform, false);
            RectTransform arrowRT = (RectTransform)arrowGO.transform;
            arrowRT.anchorMin = new Vector2(1f, 0.5f);
            arrowRT.anchorMax = new Vector2(1f, 0.5f);
            arrowRT.pivot = new Vector2(1f, 0.5f);
            arrowRT.sizeDelta = new Vector2(20f, 20f);
            arrowRT.anchoredPosition = new Vector2(-4f, 0f);
            TextMeshProUGUI arrowText = arrowGO.AddComponent<TextMeshProUGUI>();
            arrowText.text = "▼";
            arrowText.fontSize = 10f;
            arrowText.color = TextColor;
            arrowText.alignment = TextAlignmentOptions.Center;

            // Template popup; inactive until opened by click.
            GameObject templateGO = new GameObject("Template", typeof(RectTransform));
            templateGO.transform.SetParent(ddGO.transform, false);
            RectTransform templateRT = (RectTransform)templateGO.transform;
            templateRT.anchorMin = new Vector2(0f, 0f);
            templateRT.anchorMax = new Vector2(1f, 0f);
            templateRT.pivot = new Vector2(0.5f, 1f);
            templateRT.anchoredPosition = new Vector2(0f, 2f);
            templateRT.sizeDelta = new Vector2(0f, PopupHeight);

            Image templateBG = templateGO.AddComponent<Image>();
            templateBG.sprite = GetRoundedSprite(SubPanelCornerRadius);
            templateBG.type = Image.Type.Sliced;
            templateBG.color = ListBackgroundColor;

            // Viewport mask clips popup to rounded shape.
            GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform));
            viewportGO.transform.SetParent(templateGO.transform, false);
            RectTransform viewportRT = (RectTransform)viewportGO.transform;
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = new Vector2(2f, 2f);
            viewportRT.offsetMax = new Vector2(-2f, -2f);
            Image viewportImg = viewportGO.AddComponent<Image>();
            viewportImg.sprite = GetRoundedSprite(SubPanelCornerRadius);
            viewportImg.type = Image.Type.Sliced;
            viewportImg.color = new Color(1f, 1f, 1f, 0.01f);
            Mask viewportMask = viewportGO.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            GameObject contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            RectTransform contentRT = (RectTransform)contentGO.transform;
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0f, 24f);

            // Item template (one row in the popup).
            GameObject itemGO = new GameObject("Item", typeof(RectTransform));
            itemGO.transform.SetParent(contentGO.transform, false);
            RectTransform itemRT = (RectTransform)itemGO.transform;
            itemRT.anchorMin = new Vector2(0f, 0.5f);
            itemRT.anchorMax = new Vector2(1f, 0.5f);
            itemRT.sizeDelta = new Vector2(0f, 24f);

            Toggle itemToggle = itemGO.AddComponent<Toggle>();

            GameObject itemBgGO = new GameObject("ItemBackground", typeof(RectTransform));
            itemBgGO.transform.SetParent(itemGO.transform, false);
            RectTransform itemBgRT = (RectTransform)itemBgGO.transform;
            itemBgRT.anchorMin = Vector2.zero;
            itemBgRT.anchorMax = Vector2.one;
            itemBgRT.offsetMin = Vector2.zero;
            itemBgRT.offsetMax = Vector2.zero;
            Image itemBgImg = itemBgGO.AddComponent<Image>();
            itemBgImg.color = Color.white;

            // Hover/select tint via Toggle color block.
            ColorBlock cb = itemToggle.colors;
            cb.normalColor = new Color(1f, 1f, 1f, 0f);
            cb.highlightedColor = new Color(RegisterColor.r, RegisterColor.g, RegisterColor.b, 0.20f);
            cb.pressedColor = new Color(RegisterColor.r, RegisterColor.g, RegisterColor.b, 0.40f);
            cb.selectedColor = new Color(1f, 1f, 1f, 0f);
            cb.disabledColor = new Color(1f, 1f, 1f, 0f);
            itemToggle.colors = cb;

            // Required by Toggle; unused visually.
            GameObject itemCheckGO = new GameObject("ItemCheckmark", typeof(RectTransform));
            itemCheckGO.transform.SetParent(itemGO.transform, false);
            RectTransform itemCheckRT = (RectTransform)itemCheckGO.transform;
            itemCheckRT.anchorMin = new Vector2(0f, 0.5f);
            itemCheckRT.anchorMax = new Vector2(0f, 0.5f);
            itemCheckRT.pivot = new Vector2(0.5f, 0.5f);
            itemCheckRT.sizeDelta = new Vector2(1f, 1f);
            itemCheckRT.anchoredPosition = Vector2.zero;
            Image itemCheckImg = itemCheckGO.AddComponent<Image>();
            itemCheckImg.color = new Color(0f, 0f, 0f, 0f);

            GameObject itemLabelGO = new GameObject("ItemLabel", typeof(RectTransform));
            itemLabelGO.transform.SetParent(itemGO.transform, false);
            RectTransform itemLabelRT = (RectTransform)itemLabelGO.transform;
            itemLabelRT.anchorMin = Vector2.zero;
            itemLabelRT.anchorMax = Vector2.one;
            itemLabelRT.offsetMin = new Vector2(8f, 0f);
            itemLabelRT.offsetMax = new Vector2(-8f, 0f);
            TextMeshProUGUI itemLabel = itemLabelGO.AddComponent<TextMeshProUGUI>();
            itemLabel.text = "Item";
            itemLabel.fontSize = 12f;
            itemLabel.color = TextColor;
            itemLabel.alignment = TextAlignmentOptions.MidlineLeft;
            itemLabel.enableWordWrapping = false;
            itemLabel.overflowMode = TextOverflowModes.Ellipsis;

            itemToggle.targetGraphic = itemBgImg;
            itemToggle.graphic = itemCheckImg;
            itemToggle.isOn = false;

            TMP_Dropdown dd = ddGO.AddComponent<TMP_Dropdown>();
            dd.template = templateRT;
            dd.captionText = captionText;
            dd.itemText = itemLabel;
            dd.targetGraphic = bg;
            dd.transition = Selectable.Transition.None;

            templateGO.SetActive(false);

            return dd;
        }

        private static void BuildInputRow(GameObject parent, out TMP_InputField field, out Button loadBtn, out Button saveBtn)
        {
            const float ButtonWidth = 56f;
            const float ButtonHeight = 24f;
            const float Gap = 6f;

            GameObject rowGO = new GameObject("PresetIORow", typeof(RectTransform));
            rowGO.transform.SetParent(parent.transform, false);
            LayoutElement rowLE = rowGO.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 28f;
            rowLE.flexibleWidth = 1f;

            saveBtn = BuildSmallButton(rowGO, "Save", RegisterColor);
            RectTransform saveRT = (RectTransform)saveBtn.transform;
            saveRT.anchorMin = new Vector2(1f, 0.5f);
            saveRT.anchorMax = new Vector2(1f, 0.5f);
            saveRT.pivot = new Vector2(1f, 0.5f);
            saveRT.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            saveRT.anchoredPosition = Vector2.zero;

            loadBtn = BuildSmallButton(rowGO, "Load", RegisterColor);
            RectTransform loadRT = (RectTransform)loadBtn.transform;
            loadRT.anchorMin = new Vector2(1f, 0.5f);
            loadRT.anchorMax = new Vector2(1f, 0.5f);
            loadRT.pivot = new Vector2(1f, 0.5f);
            loadRT.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            loadRT.anchoredPosition = new Vector2(-(ButtonWidth + Gap), 0f);

            field = BuildInputField(rowGO, "JSON Name…");
            RectTransform fieldRT = (RectTransform)field.transform;
            fieldRT.anchorMin = new Vector2(0f, 0.5f);
            fieldRT.anchorMax = new Vector2(1f, 0.5f);
            fieldRT.pivot = new Vector2(0f, 0.5f);
            fieldRT.sizeDelta = new Vector2(-(2f * ButtonWidth + 2f * Gap), ButtonHeight);
            fieldRT.anchoredPosition = Vector2.zero;
        }

        private static Button BuildSmallButton(GameObject parent, string label, Color color)
        {
            GameObject btnGO = new GameObject(label + "Button", typeof(RectTransform));
            btnGO.transform.SetParent(parent.transform, false);

            Image img = btnGO.AddComponent<Image>();
            img.sprite = GetRoundedSprite(SubPanelCornerRadius);
            img.type = Image.Type.Sliced;
            img.color = color;

            Button btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = img;

            GameObject labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(btnGO.transform, false);
            RectTransform labelRT = (RectTransform)labelGO.transform;
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            TextMeshProUGUI text = labelGO.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 12f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            return btn;
        }

        private static TMP_InputField BuildInputField(GameObject parent, string placeholder)
        {
            GameObject fieldGO = new GameObject("InputField", typeof(RectTransform));
            fieldGO.transform.SetParent(parent.transform, false);

            Image bg = fieldGO.AddComponent<Image>();
            bg.sprite = GetRoundedSprite(SubPanelCornerRadius);
            bg.type = Image.Type.Sliced;
            bg.color = InputBackgroundColor;

            GameObject textAreaGO = new GameObject("TextArea", typeof(RectTransform));
            textAreaGO.transform.SetParent(fieldGO.transform, false);
            RectTransform textAreaRT = (RectTransform)textAreaGO.transform;
            textAreaRT.anchorMin = Vector2.zero;
            textAreaRT.anchorMax = Vector2.one;
            textAreaRT.offsetMin = new Vector2(8f, 2f);
            textAreaRT.offsetMax = new Vector2(-8f, -2f);
            textAreaGO.AddComponent<RectMask2D>();

            GameObject placeholderGO = new GameObject("Placeholder", typeof(RectTransform));
            placeholderGO.transform.SetParent(textAreaGO.transform, false);
            RectTransform placeholderRT = (RectTransform)placeholderGO.transform;
            placeholderRT.anchorMin = Vector2.zero;
            placeholderRT.anchorMax = Vector2.one;
            placeholderRT.offsetMin = Vector2.zero;
            placeholderRT.offsetMax = Vector2.zero;
            TextMeshProUGUI placeholderText = placeholderGO.AddComponent<TextMeshProUGUI>();
            placeholderText.text = placeholder;
            placeholderText.fontSize = 12f;
            placeholderText.color = PlaceholderColor;
            placeholderText.alignment = TextAlignmentOptions.MidlineLeft;
            placeholderText.fontStyle = FontStyles.Italic;
            placeholderText.enableWordWrapping = false;

            GameObject textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(textAreaGO.transform, false);
            RectTransform textRT = (RectTransform)textGO.transform;
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            TextMeshProUGUI text = textGO.AddComponent<TextMeshProUGUI>();
            text.text = "";
            text.fontSize = 12f;
            text.color = TextColor;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = false;

            TMP_InputField field = fieldGO.AddComponent<TMP_InputField>();
            field.textViewport = textAreaRT;
            field.textComponent = text;
            field.placeholder = placeholderText;
            field.targetGraphic = bg;
            field.fontAsset = text.font;
            field.pointSize = 12f;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.contentType = TMP_InputField.ContentType.Standard;
            field.characterLimit = 64;
            field.caretColor = TextColor;
            field.selectionColor = new Color(RegisterColor.r, RegisterColor.g, RegisterColor.b, 0.4f);
            // Skip Selectable tint so focus doesn't darken the bg.
            field.transition = Selectable.Transition.None;

            return field;
        }

        // Accepts 6 hex chars (no leading #).
        private static Color HexColor(string hex)
        {
            int r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            int g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            int b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        // Sliced sprite tinted by Image.color; border = radius.
        private static Sprite GetRoundedSprite(int radius)
        {
            Sprite cached;
            if (_roundedSpriteCache.TryGetValue(radius, out cached)) return cached;

            int size = Mathf.Max(radius * 4, radius * 2 + 2);
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Min(x, size - 1 - x);
                    int dy = Mathf.Min(y, size - 1 - y);
                    float alpha = 1f;
                    if (dx < radius && dy < radius)
                    {
                        float cx = radius - dx - 0.5f;
                        float cy = radius - dy - 0.5f;
                        float dist = Mathf.Sqrt(cx * cx + cy * cy);
                        if (dist > radius) alpha = 0f;
                        else alpha = Mathf.Clamp01(radius - dist);
                    }
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));

            _roundedSpriteCache[radius] = sprite;
            return sprite;
        }
    }
}
