using System;
using System.Collections.Generic;
using System.Globalization;
using ReactorSim.Game;
using UnityEngine;
using UnityEngine.UI;

namespace ReactorGame.Unity
{
    /// <summary>
    /// Presentation-only view of the synthetic CANDU core. The view owns the
    /// uGUI objects and the current immutable projection used for display;
    /// the runtime adapter remains the sole owner of simulation state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoreMapView : MonoBehaviour
    {
        public const uint ChannelCount = GameCorePresentationConstants.ChannelCount;
        public const uint BundlePositionCount = GameCorePresentationConstants.BundlePositionCount;
        public const int GridWidth = GameCorePresentationConstants.GridWidth;
        public const int GridHeight = GameCorePresentationConstants.GridHeight;
        public const int DefaultSelectedChannelIndex = 190;
        public const string TowardEndADirectionId = Phase10ControlsView.TowardEndADirectionId;
        public const string TowardEndBDirectionId = Phase10ControlsView.TowardEndBDirectionId;
        public const string DirectionEndAActionKey = "DirectionEndA";
        public const string DirectionEndBActionKey = "DirectionEndB";
        public const string ShiftFourActionKey = "ShiftFour";
        public const string ShiftEightActionKey = "ShiftEight";
        public const string RefuelActionKey = "RefuelSelected";

        private static readonly Color EmptyCellColor = new Color(0.035f, 0.075f, 0.095f, 1.0f);
        private static readonly Color UnavailableCellColor = new Color(0.025f, 0.045f, 0.060f, 1.0f);
        private static readonly Color BurnupGreen = new Color(0.18f, 0.78f, 0.42f, 1.0f);
        private static readonly Color BurnupYellow = new Color(0.96f, 0.78f, 0.18f, 1.0f);
        private static readonly Color BurnupRed = new Color(0.90f, 0.22f, 0.20f, 1.0f);
        private static readonly Color SelectedOutlineColor = new Color(0.55f, 0.94f, 1.0f, 1.0f);

        private readonly Dictionary<uint, Button> _channelButtons =
            new Dictionary<uint, Button>();
        private readonly Dictionary<uint, Image> _channelImages =
            new Dictionary<uint, Image>();
        private readonly Dictionary<uint, Outline> _channelOutlines =
            new Dictionary<uint, Outline>();
        private readonly Dictionary<string, Button> _actionButtons =
            new Dictionary<string, Button>();
        private readonly Dictionary<string, Image> _actionImages =
            new Dictionary<string, Image>();
        private readonly List<Text> _bundleDetailTexts = new List<Text>();
        private readonly List<Image> _bundleDetailImages = new List<Image>();

        private readonly RectTransform[] _gridSlots = new RectTransform[GridWidth * GridHeight];
        private readonly Image[] _gridSlotImages = new Image[GridWidth * GridHeight];
        private readonly int[] _gridSlotOccupants = new int[GridWidth * GridHeight];
        private readonly GameChannelPresentationSnapshot[] _liveChannels =
            new GameChannelPresentationSnapshot[(int)ChannelCount];

        private Phase8UnityRuntimeAdapter _runtimeAdapter;
        private Phase8UnityPresentationSnapshotV1 _snapshot;
        private RectTransform _coreMapRoot;
        private RectTransform _mapGrid;
        private RectTransform _channelPool;
        private Text _selectedChannelText;
        private Text _bundleHeading;
        private Text _refuelFeedbackText;
        private Text _statusText;
        private InputField _fuelTypeInput;
        private bool _isBuilt;
        private bool _hasRenderableCore;
        private int _selectedChannelIndex = -1;
        private string _selectedRefuellingDirectionId = TowardEndADirectionId;
        private ushort _selectedShiftCount = 4;

        public bool IsBuilt
        {
            get { return _isBuilt; }
        }

        public bool IsBound
        {
            get { return _runtimeAdapter != null; }
        }

        public int SelectedChannelIndex
        {
            get { return _selectedChannelIndex; }
        }

        public int ChannelButtonCount
        {
            get { return _channelButtons.Count; }
        }

        public int BundleDetailCount
        {
            get { return _bundleDetailTexts.Count; }
        }

        public string SelectedChannelText
        {
            get { return GetText(_selectedChannelText); }
        }

        public string SelectedRefuellingDirectionId
        {
            get { return _selectedRefuellingDirectionId; }
        }

        public ushort SelectedShiftCount
        {
            get { return _selectedShiftCount; }
        }

        public string RefuelFeedbackText
        {
            get { return GetText(_refuelFeedbackText); }
        }

        public string StatusText
        {
            get { return GetText(_statusText); }
        }

        public Phase8UnityPresentationSnapshotV1 Snapshot
        {
            get { return _snapshot; }
        }

        private void Awake()
        {
            if (GetComponent<Phase10ShellView>() != null)
            {
                EnsureVisuals();
            }
        }

        /// <summary>
        /// Builds the legacy uGUI surface once. It is public so a host that
        /// creates the shell procedurally can explicitly build presentation
        /// before binding a runtime adapter.
        /// </summary>
        public void EnsureVisuals()
        {
            if (_isBuilt)
            {
                return;
            }

            Phase10ShellView shell = GetComponent<Phase10ShellView>();
            if (shell == null)
            {
                throw new InvalidOperationException(
                    "The Core Map must be attached to a Phase10ShellView.");
            }

            shell.BuildVisualShell();
            if (!shell.TryGetPageRoot(
                    Phase10ShellPageV1.CoreMap,
                    out RectTransform pageRoot))
            {
                throw new InvalidOperationException(
                    "The Phase 10 shell has no Core Map page root.");
            }

            _coreMapRoot = CreateRect("CoreMapData", pageRoot);
            SetAnchors(_coreMapRoot, new Vector2(0.025f, 0.035f), new Vector2(0.975f, 0.965f));
            SetOffsets(_coreMapRoot, 0.0f, 0.0f, 0.0f, 0.0f);

            Image rootImage = _coreMapRoot.gameObject.AddComponent<Image>();
            rootImage.color = new Color(0.018f, 0.055f, 0.072f, 0.97f);
            rootImage.raycastTarget = false;

            VerticalLayoutGroup rootLayout = _coreMapRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.spacing = 8.0f;
            rootLayout.padding = new RectOffset(18, 18, 14, 14);
            rootLayout.childAlignment = TextAnchor.UpperLeft;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            AddValueLabel(
                _coreMapRoot,
                "CoreMapHeading",
                "380-channel core map | burnup heat map",
                24,
                TextAnchor.MiddleLeft,
                34.0f);

            RectTransform body = CreateRect("CoreMapBody", _coreMapRoot);
            LayoutElement bodyLayout = body.gameObject.AddComponent<LayoutElement>();
            bodyLayout.flexibleHeight = 1.0f;
            bodyLayout.minHeight = 620.0f;

            HorizontalLayoutGroup bodyLayoutGroup = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            bodyLayoutGroup.spacing = 16.0f;
            bodyLayoutGroup.childAlignment = TextAnchor.UpperLeft;
            bodyLayoutGroup.childControlWidth = true;
            bodyLayoutGroup.childControlHeight = true;
            bodyLayoutGroup.childForceExpandWidth = false;
            bodyLayoutGroup.childForceExpandHeight = true;

            CreateMapPanel(body);
            CreateDetailsPanel(body);

            _isBuilt = true;
            ClearMapPlacement();
            RenderUnavailableCore();
            SetControlAvailability(false);
        }

        public void Bind(Phase8UnityRuntimeAdapter runtimeAdapter)
        {
            if (runtimeAdapter == null)
            {
                throw new ArgumentNullException(nameof(runtimeAdapter));
            }

            Phase8UnityPresentationSnapshotV1 snapshot = runtimeAdapter.Snapshot;
            if (snapshot == null)
            {
                throw new InvalidOperationException(
                    "The Core Map requires a bound adapter with a snapshot.");
            }

            EnsureVisuals();
            Unsubscribe();
            _runtimeAdapter = runtimeAdapter;
            _runtimeAdapter.SnapshotChanged += HandleSnapshotChanged;
            _runtimeAdapter.CommandCompleted += HandleCommandCompleted;
            Render(snapshot);
            SetStatus(
                snapshot.Core == null
                    ? "Status: Core Map bound; this legacy snapshot has no core projection."
                    : "Status: Core Map ready; select a channel and issue a refuelling order.");
        }

        public void Unbind()
        {
            Unsubscribe();
            _runtimeAdapter = null;
            _snapshot = null;
            _selectedChannelIndex = -1;
            RenderUnavailableCore();
            ClearPresentation();
            SetControlAvailability(false);
        }

        public bool TryGetChannelButton(uint channelIndex, out Button button)
        {
            return _channelButtons.TryGetValue(channelIndex, out button);
        }

        public bool TryGetActionButton(string actionKey, out Button button)
        {
            return _actionButtons.TryGetValue(actionKey, out button);
        }

        public bool SelectChannel(uint channelIndex)
        {
            if (!_hasRenderableCore || channelIndex >= ChannelCount ||
                _liveChannels[(int)channelIndex] == null)
            {
                SetStatus(
                    "Selection rejected: channel " +
                    channelIndex.ToString(CultureInfo.InvariantCulture) +
                    " is not available in the current core projection.");
                return false;
            }

            _selectedChannelIndex = (int)channelIndex;
            if (_refuelFeedbackText != null)
            {
                _refuelFeedbackText.text =
                    "Refuel order: " + DescribeRefuellingSelection() + ".";
            }

            ApplySelectionVisuals();
            SetStatus(
                "Status: selected channel " +
                channelIndex.ToString(CultureInfo.InvariantCulture) + ".");
            return true;
        }

        public bool SelectChannel(int channelIndex)
        {
            if (channelIndex < 0)
            {
                SetStatus("Selection rejected: channel index must be nonnegative.");
                return false;
            }

            return SelectChannel((uint)channelIndex);
        }

        public bool SelectRefuellingDirection(string directionId)
        {
            if (directionId != TowardEndADirectionId && directionId != TowardEndBDirectionId)
            {
                SetStatus("Status: direction must be toward End A or End B.");
                return false;
            }

            _selectedRefuellingDirectionId = directionId;
            ApplyRefuellingChoiceVisuals();
            SetStatus("Status: refuelling direction set to " + DescribeDirection(directionId) + ".");
            return true;
        }

        public bool SelectShiftCount(ushort shiftCount)
        {
            if (shiftCount != 4 && shiftCount != 8)
            {
                SetStatus("Status: bundle count must be 4 or 8.");
                return false;
            }

            _selectedShiftCount = shiftCount;
            ApplyRefuellingChoiceVisuals();
            SetStatus(
                "Status: refuelling order set to " +
                shiftCount.ToString(CultureInfo.InvariantCulture) + " bundles.");
            return true;
        }

        public bool SelectTowardEndA()
        {
            return SelectRefuellingDirection(TowardEndADirectionId);
        }

        public bool SelectTowardEndB()
        {
            return SelectRefuellingDirection(TowardEndBDirectionId);
        }

        public bool SelectFourBundles()
        {
            return SelectShiftCount(4);
        }

        public bool SelectEightBundles()
        {
            return SelectShiftCount(8);
        }

        public Phase8UnityCommandResultV1 RefuelSelected()
        {
            return Refuel();
        }

        public Phase8UnityCommandResultV1 RefuelTowardEndA()
        {
            if (!SelectTowardEndA())
            {
                return null;
            }

            return RefuelSelected();
        }

        public Phase8UnityCommandResultV1 RefuelTowardEndB()
        {
            if (!SelectTowardEndB())
            {
                return null;
            }

            return RefuelSelected();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void HandleSnapshotChanged(Phase8UnityPresentationSnapshotV1 snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            Render(snapshot);
        }

        private void HandleCommandCompleted(Phase8UnityCommandResultV1 result)
        {
            if (result == null)
            {
                SetStatus("Status: command rejected because the adapter returned no result.");
                return;
            }

            string sequence = result.Sequence.ToString(CultureInfo.InvariantCulture);
            string message = result.Message ?? string.Empty;
            if (result.Accepted)
            {
                if (result.Kind == Phase8UnityCommandKindV1.RefuelChannel)
                {
                    _refuelFeedbackText.text = string.IsNullOrWhiteSpace(message)
                        ? "Refuel accepted (sequence " + sequence + ")."
                        : "Refuel accepted: " + message;
                    SetStatus(
                        "Status: REFUEL accepted (sequence " + sequence + ")." +
                        (string.IsNullOrWhiteSpace(message) ? string.Empty : " " + message));
                }
                else
                {
                    SetStatus(
                        "Status: last command " + result.Kind +
                        " accepted (sequence " + sequence + ")." +
                        (string.IsNullOrWhiteSpace(message) ? string.Empty : " " + message));
                }

                RenderSelectedChannel();
                return;
            }

            if (result.Kind == Phase8UnityCommandKindV1.RefuelChannel)
            {
                _refuelFeedbackText.text =
                    "Refuel rejected [" + result.DiagnosticCode + "] " +
                    result.DiagnosticMessage;
            }

            SetStatus(
                "Status: last command " + result.Kind +
                " rejected [" + result.DiagnosticCode + "] " +
                result.DiagnosticMessage);
            RenderSelectedChannel();
        }

        private void Render(Phase8UnityPresentationSnapshotV1 snapshot)
        {
            _snapshot = snapshot;
            if (snapshot == null || snapshot.Core == null)
            {
                RenderUnavailableCore();
                SetControlAvailability(false);
                return;
            }

            RenderCore(snapshot.Core);
            SetControlAvailability(_hasRenderableCore);
        }

        private void RenderCore(GameCorePresentationSnapshot core)
        {
            ClearMapPlacement();
            for (int index = 0; index < _liveChannels.Length; index++)
            {
                _liveChannels[index] = null;
            }

            if (core == null || core.Channels == null || core.Channels.Count == 0)
            {
                RenderUnavailableCore();
                return;
            }

            bool[] occupiedSlots = new bool[_gridSlots.Length];
            foreach (GameChannelPresentationSnapshot channel in core.Channels)
            {
                if (channel == null || channel.ChannelIndex >= ChannelCount)
                {
                    continue;
                }

                uint channelIndex = channel.ChannelIndex;
                _liveChannels[(int)channelIndex] = channel;
                Button button = _channelButtons[channelIndex];
                Image image = _channelImages[channelIndex];
                image.color = BurnupColor(channel.AverageBurnupMwDayPerKg);

                int column = channel.GridColumn;
                int row = channel.GridRow;
                if (column < 0 || column >= GridWidth || row < 0 || row >= GridHeight)
                {
                    continue;
                }

                int slotIndex = row * GridWidth + column;
                if (occupiedSlots[slotIndex])
                {
                    continue;
                }

                occupiedSlots[slotIndex] = true;
                _gridSlotOccupants[slotIndex] = (int)channelIndex;
                button.transform.SetParent(_gridSlots[slotIndex], false);
                SetAnchors(button.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
                SetOffsets(button.GetComponent<RectTransform>(), 2.0f, 2.0f, 2.0f, 2.0f);
                _gridSlotImages[slotIndex].color = Color.clear;
            }

            _hasRenderableCore = false;
            int selected = _selectedChannelIndex;
            if (selected < 0 || selected >= _liveChannels.Length || _liveChannels[selected] == null)
            {
                selected = DefaultSelectedChannelIndex < _liveChannels.Length &&
                    _liveChannels[DefaultSelectedChannelIndex] != null
                    ? DefaultSelectedChannelIndex
                    : FirstAvailableChannel();
            }

            if (selected >= 0)
            {
                _selectedChannelIndex = selected;
                _hasRenderableCore = true;
            }
            else
            {
                _selectedChannelIndex = -1;
            }

            ApplySelectionVisuals();
        }

        private int FirstAvailableChannel()
        {
            for (int index = 0; index < _liveChannels.Length; index++)
            {
                if (_liveChannels[index] != null)
                {
                    return index;
                }
            }

            return -1;
        }

        private void ApplySelectionVisuals()
        {
            foreach (KeyValuePair<uint, Outline> item in _channelOutlines)
            {
                item.Value.enabled = _hasRenderableCore &&
                    (int)item.Key == _selectedChannelIndex;
                item.Value.effectColor = SelectedOutlineColor;
            }

            ApplyRefuellingChoiceVisuals();
            RenderSelectedChannel();
            SetControlAvailability(_runtimeAdapter != null && _hasRenderableCore);
        }

        private void RenderSelectedChannel()
        {
            if (_selectedChannelIndex < 0 || _selectedChannelIndex >= _liveChannels.Length ||
                _liveChannels[_selectedChannelIndex] == null)
            {
                _selectedChannelText.text = "Channel: unavailable";
                RenderBundleDetails(null);
                return;
            }

            GameChannelPresentationSnapshot liveChannel = _liveChannels[_selectedChannelIndex];
            _selectedChannelText.text =
                "Channel " + _selectedChannelIndex.ToString(CultureInfo.InvariantCulture) +
                " | Grid (" + liveChannel.GridColumn.ToString(CultureInfo.InvariantCulture) +
                ", " + liveChannel.GridRow.ToString(CultureInfo.InvariantCulture) + ")" +
                " | Avg burnup " + Format(liveChannel.AverageBurnupMwDayPerKg) + " MWd/kg" +
                " | Power " + Format(liveChannel.PowerWatts) + " W" +
                " | Local power " + FormatPercent(liveChannel.LocalPowerFraction) +
                " | Tilt " + FormatPercent(liveChannel.LocalTiltFraction);

            RenderBundleDetails(liveChannel);
        }

        private void RenderBundleDetails(GameChannelPresentationSnapshot channel)
        {
            _bundleHeading.text = "12-bundle profile";

            var bundles = new List<GameBundlePresentationSnapshot>();
            if (channel != null && channel.Bundles != null)
            {
                foreach (GameBundlePresentationSnapshot bundle in channel.Bundles)
                {
                    if (bundle != null)
                    {
                        bundles.Add(bundle);
                    }
                }
            }

            bundles.Sort(CompareBundles);
            for (int index = 0; index < _bundleDetailTexts.Count; index++)
            {
                if (index >= bundles.Count)
                {
                    _bundleDetailTexts[index].text =
                        "Bundle " + (index + 1).ToString("00", CultureInfo.InvariantCulture) +
                        "\nunavailable";
                    _bundleDetailImages[index].color = UnavailableCellColor;
                    continue;
                }

                GameBundlePresentationSnapshot bundle = bundles[index];
                _bundleDetailTexts[index].text =
                    "B" + (bundle.Position + 1).ToString("00", CultureInfo.InvariantCulture) +
                    "\n" + Format(bundle.CurrentBurnupMwDayPerKg) + " MWd/kg" +
                    "\n" + bundle.FuelTypeId +
                    "\nt=" + Format(bundle.InsertedAtSeconds) + " s" +
                    "\n" + bundle.BundleId;
                _bundleDetailImages[index].color = BurnupColor(bundle.CurrentBurnupMwDayPerKg);
            }
        }

        private Phase8UnityCommandResultV1 Refuel()
        {
            if (!TryReadRefuellingInputs(
                    out uint channelIndex,
                    out ushort shiftCount,
                    out string fuelTypeId))
            {
                return null;
            }

            return Dispatch(
                delegate
                {
                    return _runtimeAdapter.RefuelChannel(
                        channelIndex,
                        _selectedRefuellingDirectionId,
                        shiftCount,
                        fuelTypeId);
                });
        }

        private bool TryReadRefuellingInputs(
            out uint channelIndex,
            out ushort shiftCount,
            out string fuelTypeId)
        {
            channelIndex = 0;
            shiftCount = _selectedShiftCount;
            fuelTypeId = _fuelTypeInput == null ? string.Empty : _fuelTypeInput.text.Trim();

            if (_runtimeAdapter == null || !_hasRenderableCore || _selectedChannelIndex < 0)
            {
                SetStatus("Status: command rejected; select a channel on a bound core map.");
                return false;
            }

            channelIndex = (uint)_selectedChannelIndex;
            if (shiftCount != 4 && shiftCount != 8)
            {
                SetStatus("Status: input rejected; bundle count must be 4 or 8.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(fuelTypeId))
            {
                SetStatus("Status: input rejected; fuel type is required.");
                return false;
            }

            return true;
        }

        private void ApplyRefuellingChoiceVisuals()
        {
            SetChoiceButtonVisual(
                DirectionEndAActionKey,
                _selectedRefuellingDirectionId == TowardEndADirectionId);
            SetChoiceButtonVisual(
                DirectionEndBActionKey,
                _selectedRefuellingDirectionId == TowardEndBDirectionId);
            SetChoiceButtonVisual(ShiftFourActionKey, _selectedShiftCount == 4);
            SetChoiceButtonVisual(ShiftEightActionKey, _selectedShiftCount == 8);
        }

        private void SetChoiceButtonVisual(string actionKey, bool isSelected)
        {
            if (!_actionButtons.ContainsKey(actionKey) || !_actionImages.ContainsKey(actionKey))
            {
                return;
            }

            Button button = _actionButtons[actionKey];
            Image image = _actionImages[actionKey];
            Color normalColor = new Color(0.095f, 0.145f, 0.215f, 1.0f);
            Color selectedColor = new Color(0.12f, 0.38f, 0.48f, 1.0f);
            Color targetColor = isSelected ? selectedColor : normalColor;
            image.color = targetColor;

            ColorBlock colors = button.colors;
            colors.normalColor = targetColor;
            colors.highlightedColor = isSelected
                ? new Color(0.18f, 0.48f, 0.56f, 1.0f)
                : new Color(0.16f, 0.29f, 0.42f, 1.0f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
        }

        private Phase8UnityCommandResultV1 Dispatch(
            Func<Phase8UnityCommandResultV1> command)
        {
            if (_runtimeAdapter == null)
            {
                SetStatus("Status: command rejected; the runtime adapter is not bound.");
                return null;
            }

            try
            {
                return command();
            }
            catch (Exception exception)
            {
                SetStatus("Status: command rejected before dispatch: " + exception.Message);
                return null;
            }
        }

        private void Unsubscribe()
        {
            if (_runtimeAdapter != null)
            {
                _runtimeAdapter.SnapshotChanged -= HandleSnapshotChanged;
                _runtimeAdapter.CommandCompleted -= HandleCommandCompleted;
            }
        }

        private void ClearPresentation()
        {
            if (!_isBuilt)
            {
                return;
            }

            _selectedChannelText.text = "Channel: unavailable";
            _bundleHeading.text = "12-bundle profile unavailable";
            _refuelFeedbackText.text = "Refuel order: unavailable";
            _statusText.text = "Status: waiting for runtime binding";
            RenderBundleDetails(null);
        }

        private void RenderUnavailableCore()
        {
            if (!_isBuilt)
            {
                return;
            }

            _hasRenderableCore = false;
            _selectedChannelIndex = -1;
            ClearMapPlacement();
            _selectedChannelText.text = "Channel: unavailable";
            _bundleHeading.text = "12-bundle profile unavailable";
            _refuelFeedbackText.text = "Refuel order: unavailable";
            RenderBundleDetails(null);
        }

        private void SetControlAvailability(bool isAvailable)
        {
            foreach (Button button in _channelButtons.Values)
            {
                button.interactable = isAvailable && _liveChannels[FindButtonChannel(button)] != null;
            }

            foreach (Button button in _actionButtons.Values)
            {
                button.interactable = isAvailable;
            }

            if (_fuelTypeInput != null)
            {
                _fuelTypeInput.interactable = isAvailable;
            }
        }

        private int FindButtonChannel(Button button)
        {
            foreach (KeyValuePair<uint, Button> item in _channelButtons)
            {
                if (item.Value == button)
                {
                    return (int)item.Key;
                }
            }

            return 0;
        }

        private void CreateMapPanel(RectTransform parent)
        {
            RectTransform mapPanel = CreateRect("CoreMapPanel", parent);
            LayoutElement panelLayout = mapPanel.gameObject.AddComponent<LayoutElement>();
            panelLayout.minWidth = 650.0f;
            panelLayout.preferredWidth = 700.0f;
            panelLayout.flexibleWidth = 1.0f;

            Image panelImage = mapPanel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.025f, 0.075f, 0.092f, 0.98f);
            panelImage.raycastTarget = false;

            VerticalLayoutGroup layout = mapPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6.0f;
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddValueLabel(
                mapPanel,
                "CoreMapPanelHeading",
                "CANDU-6 face | channels 0-379",
                18,
                TextAnchor.MiddleLeft,
                30.0f);

            _mapGrid = CreateRect("ChannelGrid22x22", mapPanel);
            LayoutElement gridLayoutElement = _mapGrid.gameObject.AddComponent<LayoutElement>();
            gridLayoutElement.minHeight = 620.0f;
            gridLayoutElement.preferredHeight = 620.0f;
            gridLayoutElement.flexibleHeight = 1.0f;

            GridLayoutGroup gridLayout = _mapGrid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(26.0f, 26.0f);
            gridLayout.spacing = new Vector2(2.0f, 2.0f);
            gridLayout.padding = new RectOffset(10, 10, 10, 10);
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = GridWidth;

            for (int row = 0; row < GridHeight; row++)
            {
                for (int column = 0; column < GridWidth; column++)
                {
                    int slotIndex = row * GridWidth + column;
                    RectTransform slot = CreateRect(
                        "CoreCell." + row.ToString(CultureInfo.InvariantCulture) +
                        "." + column.ToString(CultureInfo.InvariantCulture),
                        _mapGrid);
                    Image slotImage = slot.gameObject.AddComponent<Image>();
                    slotImage.color = EmptyCellColor;
                    slotImage.raycastTarget = false;
                    _gridSlots[slotIndex] = slot;
                    _gridSlotImages[slotIndex] = slotImage;
                }
            }

            _channelPool = CreateRect("ChannelButtonPool", mapPanel);
            SetAnchors(_channelPool, Vector2.zero, Vector2.zero);
            SetOffsets(_channelPool, 0.0f, 0.0f, 0.0f, 0.0f);
            CreateChannelButtons();
            _channelPool.gameObject.SetActive(false);

            AddValueLabel(
                mapPanel,
                "CoreMapLegend",
                "Burnup: green < 4 | yellow nominal | red > 8 MWd/kg HM",
                14,
                TextAnchor.MiddleLeft,
                28.0f);
        }

        private void CreateDetailsPanel(RectTransform parent)
        {
            RectTransform detailsPanel = CreateRect("CoreMapDetails", parent);
            LayoutElement panelLayout = detailsPanel.gameObject.AddComponent<LayoutElement>();
            panelLayout.minWidth = 720.0f;
            panelLayout.preferredWidth = 820.0f;
            panelLayout.flexibleWidth = 1.0f;

            Image panelImage = detailsPanel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.025f, 0.055f, 0.085f, 0.98f);
            panelImage.raycastTarget = false;

            VerticalLayoutGroup layout = detailsPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 7.0f;
            layout.padding = new RectOffset(16, 16, 12, 12);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddValueLabel(
                detailsPanel,
                "CoreMapDetailsHeading",
                "Selected channel details",
                20,
                TextAnchor.MiddleLeft,
                32.0f);
            _selectedChannelText = AddValueLabel(
                detailsPanel,
                "SelectedChannelText",
                "Channel: unavailable",
                15,
                TextAnchor.MiddleLeft,
                52.0f);
            _bundleHeading = AddValueLabel(
                detailsPanel,
                "BundleProfileHeading",
                "12-bundle profile unavailable",
                16,
                TextAnchor.MiddleLeft,
                28.0f);

            RectTransform bundleRow = CreateRect("BundleDetails", detailsPanel);
            LayoutElement bundleRowLayout = bundleRow.gameObject.AddComponent<LayoutElement>();
            bundleRowLayout.minHeight = 148.0f;
            bundleRowLayout.preferredHeight = 148.0f;
            HorizontalLayoutGroup bundleLayout = bundleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            bundleLayout.spacing = 3.0f;
            bundleLayout.childAlignment = TextAnchor.UpperLeft;
            bundleLayout.childControlWidth = true;
            bundleLayout.childControlHeight = true;
            bundleLayout.childForceExpandWidth = true;
            bundleLayout.childForceExpandHeight = true;

            for (int index = 0; index < (int)BundlePositionCount; index++)
            {
                RectTransform bundleCard = CreateRect(
                    "BundleDetail." + (index + 1).ToString("00", CultureInfo.InvariantCulture),
                    bundleRow);
                LayoutElement cardLayout = bundleCard.gameObject.AddComponent<LayoutElement>();
                cardLayout.minWidth = 50.0f;
                cardLayout.flexibleWidth = 1.0f;

                Image cardImage = bundleCard.gameObject.AddComponent<Image>();
                cardImage.color = UnavailableCellColor;
                cardImage.raycastTarget = false;
                _bundleDetailImages.Add(cardImage);

                Text detail = AddValueLabel(
                    bundleCard,
                    "Text",
                    "Bundle " + (index + 1).ToString("00", CultureInfo.InvariantCulture) +
                    "\nunavailable",
                    11,
                    TextAnchor.MiddleCenter,
                    144.0f);
                SetAnchors(detail.rectTransform, Vector2.zero, Vector2.one);
                SetOffsets(detail.rectTransform, 2.0f, 2.0f, 2.0f, 2.0f);
                detail.horizontalOverflow = HorizontalWrapMode.Wrap;
                detail.verticalOverflow = VerticalWrapMode.Truncate;
                _bundleDetailTexts.Add(detail);
            }

            AddValueLabel(
                detailsPanel,
                "RefuellingOrderHeading",
                "Direct refuelling order",
                16,
                TextAnchor.MiddleLeft,
                28.0f);

            RectTransform directionRow = CreateRow("RefuellingDirection", detailsPanel, 62.0f);
            AddValueLabel(
                directionRow,
                "RefuellingInputLabel",
                "Direction",
                15,
                TextAnchor.MiddleLeft,
                62.0f).GetComponent<LayoutElement>().preferredWidth = 150.0f;
            AddValueLabel(
                directionRow,
                "DirectionSelection",
                "Choose fuelling direction",
                13,
                TextAnchor.MiddleLeft,
                62.0f).GetComponent<LayoutElement>().preferredWidth = 190.0f;
            AddActionButton(
                directionRow,
                DirectionEndAActionKey,
                "END A",
                delegate { SelectTowardEndA(); });
            AddActionButton(
                directionRow,
                DirectionEndBActionKey,
                "END B",
                delegate { SelectTowardEndB(); });

            RectTransform shiftRow = CreateRow("RefuellingBundleCount", detailsPanel, 62.0f);
            AddValueLabel(
                shiftRow,
                "BundleCountLabel",
                "Bundles",
                15,
                TextAnchor.MiddleLeft,
                62.0f).GetComponent<LayoutElement>().preferredWidth = 150.0f;
            AddValueLabel(
                shiftRow,
                "BundleCountSelection",
                "Choose shift size",
                13,
                TextAnchor.MiddleLeft,
                62.0f).GetComponent<LayoutElement>().preferredWidth = 190.0f;
            AddActionButton(
                shiftRow,
                ShiftFourActionKey,
                "4 BUNDLES",
                delegate { SelectFourBundles(); });
            AddActionButton(
                shiftRow,
                ShiftEightActionKey,
                "8 BUNDLES",
                delegate { SelectEightBundles(); });

            RectTransform fuelRow = CreateRow("RefuellingFuel", detailsPanel, 60.0f);
            AddValueLabel(
                fuelRow,
                "FuelLabel",
                "Fuel type",
                15,
                TextAnchor.MiddleLeft,
                60.0f).GetComponent<LayoutElement>().preferredWidth = 150.0f;
            _fuelTypeInput = CreateInputField(
                fuelRow,
                "RefuelFuelType",
                InputField.ContentType.Standard,
                "NAT-U-SYNTHETIC",
                230.0f);

            RectTransform actionRow = CreateRow("RefuellingAction", detailsPanel, 70.0f);
            AddActionButton(
                actionRow,
                RefuelActionKey,
                "REFUEL SELECTED CHANNEL",
                delegate { RefuelSelected(); });

            _refuelFeedbackText = AddValueLabel(
                detailsPanel,
                "CoreMapRefuelFeedback",
                "Refuel order: End A | 4 bundles",
                14,
                TextAnchor.MiddleLeft,
                48.0f);
            _statusText = AddValueLabel(
                detailsPanel,
                "CoreMapStatus",
                "Status: waiting for runtime binding",
                14,
                TextAnchor.MiddleLeft,
                58.0f);

            ApplyRefuellingChoiceVisuals();
        }

        private void CreateChannelButtons()
        {
            for (uint channelIndex = 0; channelIndex < ChannelCount; channelIndex++)
            {
                uint selectedIndex = channelIndex;
                RectTransform buttonRoot = CreateRect(
                    "Channel." + channelIndex.ToString(CultureInfo.InvariantCulture),
                    _channelPool);
                Image image = buttonRoot.gameObject.AddComponent<Image>();
                image.color = UnavailableCellColor;
                image.raycastTarget = true;

                Button button = buttonRoot.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.86f, 0.98f, 1.0f, 1.0f);
                colors.pressedColor = new Color(0.62f, 0.90f, 1.0f, 1.0f);
                colors.selectedColor = colors.highlightedColor;
                button.colors = colors;

                Text label = AddValueLabel(
                    buttonRoot,
                    "Label",
                    "C" + channelIndex.ToString(CultureInfo.InvariantCulture),
                    10,
                    TextAnchor.MiddleCenter,
                    26.0f);
                SetAnchors(label.rectTransform, Vector2.zero, Vector2.one);
                SetOffsets(label.rectTransform, 1.0f, 1.0f, 1.0f, 1.0f);
                label.color = Color.white;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Truncate;

                Outline outline = buttonRoot.gameObject.AddComponent<Outline>();
                outline.effectColor = SelectedOutlineColor;
                outline.effectDistance = new Vector2(2.0f, 2.0f);
                outline.enabled = false;

                button.onClick.AddListener(delegate { SelectChannel(selectedIndex); });
                _channelButtons.Add(channelIndex, button);
                _channelImages.Add(channelIndex, image);
                _channelOutlines.Add(channelIndex, outline);
            }
        }

        private Button AddActionButton(
            RectTransform parent,
            string key,
            string labelText,
            Action callback)
        {
            RectTransform buttonRoot = CreateRect("Button." + key, parent);
            LayoutElement buttonLayout = buttonRoot.gameObject.AddComponent<LayoutElement>();
            buttonLayout.minHeight = 58.0f;
            buttonLayout.preferredHeight = 58.0f;
            buttonLayout.preferredWidth = 250.0f;
            buttonLayout.flexibleWidth = 1.0f;

            Image image = buttonRoot.gameObject.AddComponent<Image>();
            image.color = new Color(0.095f, 0.145f, 0.215f, 1.0f);
            image.raycastTarget = true;

            Button button = buttonRoot.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.095f, 0.145f, 0.215f, 1.0f);
            colors.highlightedColor = new Color(0.16f, 0.29f, 0.42f, 1.0f);
            colors.pressedColor = new Color(0.23f, 0.42f, 0.54f, 1.0f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            Text label = AddValueLabel(
                buttonRoot,
                "Label",
                labelText,
                14,
                TextAnchor.MiddleCenter,
                58.0f);
            SetAnchors(label.rectTransform, Vector2.zero, Vector2.one);
            SetOffsets(label.rectTransform, 8.0f, 0.0f, 8.0f, 0.0f);
            button.onClick.AddListener(delegate { callback(); });
            _actionButtons.Add(key, button);
            _actionImages.Add(key, image);
            return button;
        }

        private InputField CreateInputField(
            RectTransform parent,
            string name,
            InputField.ContentType contentType,
            string initialValue,
            float preferredWidth)
        {
            RectTransform inputRoot = CreateRect(name, parent);
            LayoutElement inputLayout = inputRoot.gameObject.AddComponent<LayoutElement>();
            inputLayout.minHeight = 58.0f;
            inputLayout.preferredHeight = 58.0f;
            inputLayout.preferredWidth = preferredWidth;

            Image background = inputRoot.gameObject.AddComponent<Image>();
            background.color = new Color(0.10f, 0.16f, 0.22f, 1.0f);
            background.raycastTarget = true;

            InputField input = inputRoot.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;
            input.contentType = contentType;
            input.lineType = InputField.LineType.SingleLine;

            RectTransform textRoot = CreateRect("Text", inputRoot);
            SetAnchors(textRoot, Vector2.zero, Vector2.one);
            SetOffsets(textRoot, 10.0f, 0.0f, 10.0f, 0.0f);
            Text text = textRoot.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 17;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            input.textComponent = text;
            input.text = initialValue;
            return input;
        }

        private static RectTransform CreateRow(
            string name,
            RectTransform parent,
            float height)
        {
            RectTransform row = CreateRect(name, parent);
            LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
            rowLayout.minHeight = height;
            rowLayout.preferredHeight = height;

            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6.0f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }

        private void ClearMapPlacement()
        {
            if (!_isBuilt || _channelPool == null)
            {
                return;
            }

            for (int index = 0; index < _gridSlots.Length; index++)
            {
                _gridSlotOccupants[index] = -1;
                if (_gridSlotImages[index] != null)
                {
                    _gridSlotImages[index].color = EmptyCellColor;
                }
            }

            foreach (Button button in _channelButtons.Values)
            {
                button.transform.SetParent(_channelPool, false);
                button.interactable = false;
            }
        }

        private static int CompareBundles(
            GameBundlePresentationSnapshot left,
            GameBundlePresentationSnapshot right)
        {
            return left.Position.CompareTo(right.Position);
        }

        private static Color BurnupColor(double burnup)
        {
            if (double.IsNaN(burnup) || double.IsInfinity(burnup))
            {
                return UnavailableCellColor;
            }

            if (burnup <= 4.0)
            {
                float amount = Mathf.Clamp01((float)((burnup - 0.0) / 4.0));
                return Color.Lerp(BurnupGreen, BurnupYellow, amount);
            }

            if (burnup <= 8.0)
            {
                float amount = Mathf.Clamp01((float)((burnup - 4.0) / 4.0));
                return Color.Lerp(BurnupYellow, BurnupRed, amount * 0.82f);
            }

            return Color.Lerp(
                BurnupRed,
                new Color(0.62f, 0.06f, 0.08f, 1.0f),
                Mathf.Clamp01((float)((burnup - 8.0) / 4.0)));
        }

        private static Text AddValueLabel(
            RectTransform parent,
            string name,
            string value,
            int fontSize,
            TextAnchor alignment,
            float height)
        {
            RectTransform labelRoot = CreateRect(name, parent);
            LayoutElement layout = labelRoot.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;

            Text label = labelRoot.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.color = new Color(0.90f, 0.95f, 1.0f, 1.0f);
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            label.text = value;
            return label;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private static void SetAnchors(
            RectTransform rect,
            Vector2 minimum,
            Vector2 maximum)
        {
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
        }

        private static void SetOffsets(
            RectTransform rect,
            float left,
            float bottom,
            float right,
            float top)
        {
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private string DescribeRefuellingSelection()
        {
            return DescribeDirection(_selectedRefuellingDirectionId) +
                " | " +
                _selectedShiftCount.ToString(CultureInfo.InvariantCulture) +
                " bundles";
        }

        private static string DescribeDirection(string directionId)
        {
            return directionId == TowardEndBDirectionId ? "End B" : "End A";
        }

        private static string GetText(Text text)
        {
            return text == null ? string.Empty : text.text;
        }

        private static string Format(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return "n/a";
            }

            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatPercent(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return "n/a";
            }

            return value.ToString("0.0%", CultureInfo.InvariantCulture);
        }

        private void SetStatus(string status)
        {
            if (_statusText != null)
            {
                _statusText.text = status;
            }
        }
    }
}
