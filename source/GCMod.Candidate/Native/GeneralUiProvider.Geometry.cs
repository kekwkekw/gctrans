#nullable enable
using System.Text.Json;
using System.Text.RegularExpressions;
using GCMod.Nextgen;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GCMod.Candidate.Native;

public sealed partial class GeneralUiProvider
{
    // These are recovered, code-owned renderer policies. Data cannot request a property write.
    enum GeometryPolicy { Exact, NaturalWidth, ProfileChange, DecorativeCluster, BadgeIntrinsic, ScopedModelLabel, ProfileOffLabel, NativeContentFit, ScopedPopupCaption, FooterComposite, ScopedHeaderPower, OptionCheckboxComposite, ScopedShopHeader, ScopedCharacterEnhance, ScopedShopHelp }
    sealed class GeometryTicket
    {
        public TextMeshProUGUI Text = null!;
        public UiBinding Binding = null!;
        public int Instance, Scene, PreparedFrame, WrittenFrame, LastCheckedFrame, Settled;
        public GeometryPolicy Policy;
        public string Display = "", Layout = "", WrittenText = "";
        public float Width, Height;
        public ClusterCapture? Cluster, PreviousCluster;
        public PhysicalTargetIdentity? Identity;
        public Gc.UnitScene.UnitProfileController? Profile;
        public int ProfileInstance;
        public long ProfileModel;
        public long ProfileChange;
        public float LastSize = 30;
        public GeometryFloatTarget? SizeTarget;
        public bool Failed;
        public bool RestoringText;
        public FilterAutoSizeState? AutoSize;
        public HeaderPowerGeometryIdentity? HeaderPower;
        public CharacterEnhanceGeometryIdentity? CharacterEnhance;
        public PopupCaptionIdentity? PopupCaption;
        public ShopHelpIdentity? ShopHelp;
        public RectTransform? ShopHelpRegion;
        public ShopHeaderGeometryIdentity? ShopHeader;
        public RectTransform? ShopHeaderContainer;
        public CampaignCaptionIdentity? CampaignCaption;
        public FilterResetGeometryIdentity? FilterReset;
        public OptionCheckboxGeometryIdentity? OptionCheckbox;
        public RectTransform? OptionCheckboxContainer;
        public long OwnerGeneration, UiGeneration;
        public int ProducerFrame;
        public Func<bool> OwnerCurrent = null!, SourceCurrent = null!, ControlsCurrent = null!;
        public string PreviousMotion = "";
        public int MotionStable;
        public UiVisualFailure Failure;
        public UiVisualField ChangedFields;
        public int Width100, Height100, VisibleGlyphs, MaterialChannels, HiddenChannels;
        public bool CallbackScopedWrite;
        public bool PrimaryHidden;
        public ContentFitDriver? ContentDriver;
        public Component? FooterOwner;
        public RectTransform? FooterContainer;
        public int FooterContainerInstance;
        public bool FooterLeafFits, FooterCompositeFits;
    }
    sealed record JpClusterSample(int Frame, int Scene, string BindingId, string Source, ClusterCapture Cluster);
    sealed record ContentFitDriver(int FitterInstance, int GroupInstance, int ContainerInstance,
        bool HorizontalPreferred, bool VerticalPreferred, bool GroupHorizontal, bool GroupVertical, RectTransform Container)
    {
        public bool SameAs(ContentFitDriver other) => FitterInstance == other.FitterInstance && GroupInstance == other.GroupInstance &&
            ContainerInstance == other.ContainerInstance && HorizontalPreferred == other.HorizontalPreferred && VerticalPreferred == other.VerticalPreferred &&
            GroupHorizontal == other.GroupHorizontal && GroupVertical == other.GroupVertical;
    }
    readonly Dictionary<long, JpClusterSample> jpGeometry = new();
    readonly Dictionary<PhysicalTargetIdentity, GeometryTicket> geometryOwned = new();
    readonly HashSet<UiGeometryHoldKey> geometryRejected = new();
    readonly Dictionary<UiGeometryHoldKey, int> transientGeometryRejected = new();
    readonly UiGeometryDiagnosticBudget geometryDiagnostics = new();

    bool TryPrepareGeometry(Owner owner, UiBinding binding, TextMeshProUGUI text, out GeometryTicket? ticket)
    {
        gate.Require();
        ticket = null;
        if (!Alive(text) || !text.enabled || !text.gameObject.activeInHierarchy) return false;
        if (text.text != binding.SourceIdentity.ExactJp)
        { RecordProfilePrepareDecline(owner, binding, text, UiVisualPhase.SourceJoin, UiVisualFailure.SourceChanged, UiVisualField.Source, 2048); return false; }
        if (geometryOwned.Count >= 128 || geometryRejected.Count >= 256 || transientGeometryRejected.Count >= 256 ||
            geometryRejected.Contains(new(binding.Id, Ptr(text), owner.Lifetime, host.Snapshot.Generations.Ui)) ||
            transientGeometryRejected.TryGetValue(new(binding.Id, Ptr(text), owner.Lifetime, host.Snapshot.Generations.Ui), out int producer) && producer == owner.Started)
            return false;
        try
        {
            if (!PreparePopupCaption(owner, binding, text, out var popupCaption)) return false;
            if (!PrepareCampaignCaption(owner, binding, text, out var campaignCaption)) return false;
            var policy = (binding.AdapterId == "common.model-label.v1" &&
                binding.Selector.ScopeKey is "equipment-sort" or "current-filter" or "element-filter") || Ui6ConsumerRoutePolicy.UsesScopedConsumerRenderer(binding)
                ? GeometryPolicy.ScopedModelLabel : GeometryPolicy.Exact;
            if (popupCaption != null && PopupCaptionPreparationPolicy.UsesScopedStatus(binding)) policy = GeometryPolicy.ScopedPopupCaption;
            if(MissionCellCaptionPolicy.Supports(binding)) {
                if(!TryMissionCellCaption(owner,binding,text,out _)||RenderedGeometryText(text)!=MeshSource(text,text.text)||text.m_havePropertiesChanged||text.m_isLayoutDirty)return false;
                policy=GeometryPolicy.ScopedPopupCaption;
            }
            Gc.UnitScene.UnitProfileController? profileOwner = null;
            if (binding.AdapterId == "ui.residual-caption.v1" && binding.Selector.SlotKey == "profile_change_button")
            {
                profileOwner = Parent<Gc.UnitScene.UnitProfileController>(text);
                if (!Alive(profileOwner) || Ptr(profileOwner!._userUnit) == 0 || !Alive(profileOwner._change) ||
                    !text.transform.IsChildOf(profileOwner._change.transform) || text.fontSize != 30 || text.enableAutoSizing ||
                    Math.Abs(text.rectTransform.rect.width - 130) > .05f || Math.Abs(text.rectTransform.rect.height - 24) > .05f)
                    return false;
                policy = GeometryPolicy.ProfileChange;
            }
            if (binding.AdapterId == "ui.residual-caption.v1" &&
                binding.Selector.SlotKey is "accessory_bulk_dismantle" or "exchange_material_deselect")
                policy = GeometryPolicy.NaturalWidth;
            bool profileTab = binding.AdapterId == "ui.caption.v1" &&
                binding.Selector.SlotKey is "profile_tab" or "profile_style_tab" or "profile_voice_tab";
            var label = profileTab ? Parent<Gc.LabelTab>(text) : null;
            if (Alive(label) && Ptr(label!.onObjectText) == Ptr(text)) policy = GeometryPolicy.DecorativeCluster;
            else if (Alive(label) && Ptr(label!.offObjectText) == Ptr(text) && !label.isSelecting)
                policy = GeometryPolicy.ProfileOffLabel;
            if (binding.AdapterId == "ui.residual-caption.v1" && binding.Selector.ScopeKey == "gc.novelkeywordpopup" &&
                binding.Selector.SlotKey == "annotation_close_caption" && owner.Value is Gc.NovelKeyWordPopup popup &&
                Alive(popup.closeButton) && Ptr(popup.closeButton.textMeshProUgui) == Ptr(text) && text.transform.IsChildOf(popup.closeButton.transform))
                policy = GeometryPolicy.ScopedPopupCaption;
            ContentFitDriver? contentDriver = null;
            if(MissionCellCaptionPolicy.Supports(binding)) {
                contentDriver=CaptureContentFitDriver(text);
                var missionCell=Parent<Gc.Mission.ContentScrollCell>(text);var missionView=Alive(missionCell)?missionCell!._content:null;
                Component? nativeControl=Alive(missionView)?binding.Selector.SlotKey=="ui2-b04"?missionView!._receive:missionView!._transition:null;
                if(contentDriver==null||!Alive(nativeControl)||!contentDriver.Container.IsChildOf(nativeControl!.transform))return false;
                policy=GeometryPolicy.NativeContentFit;
            }
            if (binding.AdapterId == "ui4.static-caption.v1" && binding.Selector.ScopeKey == "gc.option.optionpopup" &&
                binding.Selector.SlotKey == "ui4-ui4-s020-26-0600c050")
            {
                contentDriver = CaptureContentFitDriver(text);
                if (contentDriver != null) policy = GeometryPolicy.NativeContentFit;
            }
            FilterResetGeometryIdentity? filterReset = null;
            if (TryFilterResetGeometry(owner.Value, binding, text, out filterReset, out var filterDriver, owner.Lifetime))
            { contentDriver = filterDriver; policy = GeometryPolicy.NativeContentFit; }
            if (binding.Selector.SlotKey == "exchange_available_badge") policy = GeometryPolicy.BadgeIntrinsic;
            HeaderPowerGeometryIdentity? headerPower = null;
            if (TryHeaderPowerGeometry(owner.Value, binding, text, out headerPower, true, owner.Lifetime)) policy = GeometryPolicy.ScopedHeaderPower;
            CharacterEnhanceGeometryIdentity? characterEnhance = null;
            if (TryCharacterEnhanceGeometry(owner.Value, binding, text, out characterEnhance, true, owner.Lifetime)) policy = GeometryPolicy.ScopedCharacterEnhance;
            ShopHelpIdentity? shopHelp = null; RectTransform? shopHelpRegion = null;
            if (ShopHelpGeometryPolicy.Supports(binding))
            {
                if (!TryShopHelpGeometry(owner.Value,binding,text,out shopHelp,out shopHelpRegion)
                    || !ShopHelpRegionProof(text,shopHelpRegion!,binding,owner.Lifetime,false)) return false;
                policy=GeometryPolicy.ScopedShopHelp;
            }
            ShopHeaderGeometryIdentity? shopHeader = null; RectTransform? shopHeaderContainer = null;
            if (TryShopHeaderGeometry(owner.Value, binding, text, out shopHeader, out shopHeaderContainer, true, owner.Lifetime))
            {
                if (!ShopHeaderContainerReady(shopHeaderContainer) || RenderedGeometryText(text) != MeshSource(text, text.text) || text.m_havePropertiesChanged || text.m_isLayoutDirty
                    || GlyphFailure(text, visibleContainer: shopHeaderContainer, fullComposite: true, shopMaterial: true) != UiVisualFailure.None) return false;
                policy = GeometryPolicy.ScopedShopHeader;
            }
            OptionCheckboxGeometryIdentity? optionCheckbox = null; RectTransform? optionCheckboxContainer = null;
            if (TryOptionCheckboxGeometry(owner.Value, binding, text, out optionCheckbox, out optionCheckboxContainer, true, owner.Lifetime))
            {
                // The native checkbox owns this exact caption through explicit graphic membership
                // or its verified declared direct-caption role. Preserve complete SDF quads inside the control.
                if (RenderedGeometryText(text) != MeshSource(text, text.text) || text.m_havePropertiesChanged || text.m_isLayoutDirty) return false;
                var checkboxBaseline = GlyphFailure(text, visibleContainer: optionCheckboxContainer, fullComposite: true);
                RecordOptionCheckboxBaseline(binding, owner.Lifetime, checkboxBaseline);
                if (checkboxBaseline != UiVisualFailure.None) return false;
                policy = GeometryPolicy.OptionCheckboxComposite;
            }
            var footer = FooterContainer(owner.Value, binding, text);
            if (footer != null)
            {
                // Accepted footer labels belong to the directly linked native button composite.
                // The native leaf, button and controls are not resized or otherwise written.
                if (!FooterBaseline(owner, binding, text, footer)) return false;
                policy = GeometryPolicy.FooterComposite;
            }
            ClusterCapture? cluster = null;
            if (policy == GeometryPolicy.DecorativeCluster)
            {
                UiVisualFailure jpFailure = UiVisualFailure.None; UiVisualField jpFields = UiVisualField.None;
                if (RenderedGeometryText(text) != MeshSource(text, text.text)) { jpFailure |= UiVisualFailure.MeshStale; jpFields |= UiVisualField.MeshSequence; }
                if (text.m_havePropertiesChanged || text.m_isLayoutDirty) { jpFailure |= UiVisualFailure.Dirty; jpFields |= UiVisualField.Dirty; }
                if (jpFailure != UiVisualFailure.None)
                { RecordProfilePrepareDecline(owner, binding, text, UiVisualPhase.JpBaseline, jpFailure, jpFields); return false; }
                cluster = CaptureCluster(text, binding.Selector.SlotKey == "profile_tab");
                // The recovered UI21 L3 contract required the supported native composite JP
                // baseline, not strict original-JP leaf glyph fit. Preserve the full source,
                // actual visibility/masks/channels and composite proof before any write.
                if (ClusterBaseline(cluster))
                {
                    UiVisualFailure glyph = OriginalJpCompositeGlyphFailure(text, cluster, out uint originalDetail);
                    if (glyph != UiVisualFailure.None)
                    {
                        UiVisualField field = (glyph & UiVisualFailure.MaskClipped) != 0 ? UiVisualField.Mask :
                            (glyph & UiVisualFailure.RenderChannelHidden) != 0 ? UiVisualField.Channel : UiVisualField.GlyphRect;
                        RecordProfilePrepareDecline(owner, binding, text, UiVisualPhase.JpBaseline, glyph, field, originalDetail);
                        return false;
                    }
                    if (originalDetail != 0)
                        RecordProfilePrepareDecline(owner, binding, text, UiVisualPhase.JpBaseline, UiVisualFailure.None, UiVisualField.GlyphRect, originalDetail | 4096);
                }
                long key = Ptr(text);
                bool stable = jpGeometry.TryGetValue(key, out var prior) && prior.Frame < frame &&
                    frame - prior.Frame <= 180 && prior.Scene == text.gameObject.scene.handle &&
                    prior.BindingId == binding.Id && prior.Source == text.text &&
                    ClusterBaseline(prior.Cluster) && ClusterBaseline(cluster) && ClusterStable(prior.Cluster, cluster);
                if (jpGeometry.Count < 256 || jpGeometry.ContainsKey(key))
                    jpGeometry[key] = new(frame, text.gameObject.scene.handle, binding.Id, text.text, cluster);
                if (!stable)
                {
                    uint flags = cluster.ProofFailure;
                    if (prior == null) flags |= 512;
                    else if (!ClusterStable(prior.Cluster, cluster)) flags |= 1024;
                    RecordProfilePrepareDecline(owner, binding, text, UiVisualPhase.ClusterBaseline, UiVisualFailure.LayoutChanged,
                        cluster.Supported ? UiVisualField.Transform : UiVisualField.LayoutDriver, flags);
                    return false;
                }
            }
            if ((policy is GeometryPolicy.DecorativeCluster or GeometryPolicy.BadgeIntrinsic) &&
                (RenderedGeometryText(text) != MeshSource(text, text.text) || text.m_havePropertiesChanged || text.m_isLayoutDirty)) return false;
            if (policy == GeometryPolicy.BadgeIntrinsic && !BadgeIntrinsicFit(text)) return false;
            // Strict leaf/truncation/full-KO glyph checks remain in TickGeometry after write.
            // Original JP for this renderer already passed the scoped composite proof above.
            if (policy == GeometryPolicy.NaturalWidth && !HasNaturalWidthDriver(text)) return false;
            var autoSize = CaptureFilterAutoSize(owner, binding, text);
            ticket = new GeometryTicket
            {
                Text = text, Binding = binding, Instance = text.GetInstanceID(), Scene = text.gameObject.scene.handle,
                PreparedFrame = frame, Policy = policy, Cluster = cluster,
                Width = text.rectTransform.rect.width, Height = text.rectTransform.rect.height,
                Display = GeometryDisplay(text, policy == GeometryPolicy.ProfileChange || autoSize != null, AllowsNativeSelectionColor(policy)), Layout = GeometryLayout(text, policy),
                AutoSize = autoSize,
                ContentDriver = contentDriver,
                ShopHelp = shopHelp, ShopHelpRegion = shopHelpRegion, PopupCaption = popupCaption, HeaderPower = headerPower, CharacterEnhance = characterEnhance, ShopHeader = shopHeader, ShopHeaderContainer = shopHeaderContainer, CampaignCaption = campaignCaption, FilterReset = filterReset, OptionCheckbox = optionCheckbox, OptionCheckboxContainer = optionCheckboxContainer,
                FooterOwner = footer == null ? null : owner.Value, FooterContainer = footer,
                FooterContainerInstance = footer == null ? 0 : footer.GetInstanceID(),
                OwnerGeneration = owner.Lifetime, ProducerFrame = owner.Started, UiGeneration = host.Snapshot.Generations.Ui,
                OwnerCurrent = () => Current(owner),
                Profile = profileOwner, ProfileInstance = Alive(profileOwner) ? profileOwner!.GetInstanceID() : 0,
                ProfileModel = Alive(profileOwner) ? Ptr(profileOwner!._userUnit) : 0,
                ProfileChange = Alive(profileOwner) ? Ptr(profileOwner!._change) : 0
            };
            RecordGeometry(ticket, UiVisualPhase.SourceJoin);
            RecordGeometry(ticket, UiVisualPhase.Prepare);
            return true;
        }
        catch { RecordProfilePrepareDecline(owner, binding, text, UiVisualPhase.Prepare, UiVisualFailure.ReadFailed, UiVisualField.Target); return false; }
    }

    void OnGeometryWritten(GeometryTicket ticket, PhysicalTargetIdentity identity)
    {
        gate.Require();
        ticket.Identity = identity;
        ticket.WrittenFrame = frame;
        ticket.WrittenText = ticket.Text.text;
        RecordGeometry(ticket, UiVisualPhase.Written);
        if (ticket.Policy == GeometryPolicy.ProfileChange)
            ticket.SizeTarget = new GeometryFloatTarget(gate, ticket.Text, () => ReadGeometrySize(ticket));
        geometryOwned[identity] = ticket;
        jpGeometry.Remove(Ptr(ticket.Text));
    }

    void TickGeometry()
    {
        gate.Require();
        foreach (var key in jpGeometry.Where(x => frame - x.Value.Frame > 180).Select(x => x.Key).ToArray())
            jpGeometry.Remove(key);
        foreach (var pair in geometryOwned.ToArray())
        {
            var t = pair.Value;
            if (t.Failed)
            {
                RestoreGeometryTicket(pair.Key, "GENERAL_GEOMETRY_RESTORE_RETRY");
                continue;
            }
            // Lifetime/source checks precede mesh/layout/native glyph reads, also after settlement.
            if (frame <= t.WrittenFrame || frame == t.LastCheckedFrame) continue;
            t.LastCheckedFrame = frame;
            try
            {
                if (!t.OwnerCurrent() || !Alive(t.Text) || t.Text.GetInstanceID() != t.Instance || t.Text.gameObject.scene.handle != t.Scene)
                {
                    FailGeometry(pair.Key, t, UiVisualFailure.OwnerRebound, UiVisualField.Target);
                    continue;
                }
                if (!t.SourceCurrent() || t.Text.text != t.WrittenText)
                { FailGeometry(pair.Key, t, UiVisualFailure.SourceChanged, UiVisualField.Source); continue; }
                if (!t.Text.gameObject.activeInHierarchy || !t.Text.enabled)
                {
                    FailGeometry(pair.Key, t, UiVisualFailure.TargetInactive, UiVisualField.Target);
                    continue;
                }
                if (t.AutoSize != null)
                {
                    var currentAutoSize = ReadAutoSize(t.Text);
                    if (!FilterOptionAutoSizePolicy.ConfigurationMatches(t.AutoSize.Configuration, currentAutoSize)
                        || t.Settled >= 2 && !FilterOptionAutoSizePolicy.WithinRange(currentAutoSize))
                    { FailGeometry(pair.Key, t, UiVisualFailure.DisplayChanged, UiVisualField.FontSize); continue; }
                }
                if (t.Settled >= 2) continue; // Retained source/owner/config guard is cheap; no settled full-tree polling.
                if (!t.ControlsCurrent())
                { FailGeometry(pair.Key, t, UiVisualFailure.ControlChanged, UiVisualField.Controls); continue; }
                string expected = MeshSource(t.Text, t.WrittenText);
                bool sequenceCurrent = RenderedGeometryText(t.Text) == expected;
                bool dirty = t.Text.m_havePropertiesChanged || t.Text.m_isLayoutDirty;
                bool meshCurrent = sequenceCurrent && !dirty;
                UiVisualFailure failure = sequenceCurrent ? UiVisualFailure.None : UiVisualFailure.MeshStale;
                UiVisualField fields = sequenceCurrent ? UiVisualField.None : UiVisualField.MeshSequence;
                if (dirty) { failure |= UiVisualFailure.Dirty; fields |= UiVisualField.Dirty; }
                if (!meshCurrent)
                {
                    var delayed = UiVisualSettlement.Assess(frame - t.WrittenFrame, failure, false);
                    t.Settled = 0;
                    if (delayed.Action == UiVisualAction.Restore) FailGeometry(pair.Key, t, delayed.Failure, fields);
                    else RecordGeometry(t, delayed.Phase, delayed.Failure, fields);
                    continue;
                }
                ClusterCapture? cluster = t.Policy == GeometryPolicy.DecorativeCluster
                    ? CaptureCluster(t.Text, t.Binding.Selector.SlotKey == "profile_tab") : null;
                string layoutNow = t.Policy == GeometryPolicy.DecorativeCluster ? "" : GeometryLayout(t.Text, t.Policy);
                bool geometry = t.Policy == GeometryPolicy.DecorativeCluster
                    ? t.Cluster != null && cluster != null && ClusterEqual(t.Cluster, cluster) &&
                        (t.PreviousCluster == null || ClusterStable(t.PreviousCluster, cluster))
                    : t.Layout == layoutNow;
                if (!geometry && layoutNow.Length > 0) fields |= UiLayoutDiagnosticFields.Compare(t.Layout, layoutNow);
                if (t.Policy == GeometryPolicy.NaturalWidth)
                {
                    float width = t.Text.rectTransform.rect.width, height = t.Text.rectTransform.rect.height;
                    geometry &= float.IsFinite(width) && float.IsFinite(height) && t.Width > 0 &&
                        Math.Abs(width - t.Width) <= 4 && Math.Abs(width - t.Width) / t.Width <= .05f &&
                        Math.Abs(height - t.Height) <= .05f && HasNaturalWidthDriver(t.Text);
                }
                if (t.Policy == GeometryPolicy.NativeContentFit)
                {
                    RecordFilterPreferredCurrent(t);
                    var driver = t.ContentDriver;
                    var currentDriver = CaptureContentFitDriver(t.Text);
                    bool sameDriver = driver != null && currentDriver != null && driver.SameAs(currentDriver);
                    bool natural = driver is not null && currentDriver is not null && driver.SameAs(currentDriver) && NativeContentFitPolicy.Matches(driver.HorizontalPreferred, driver.VerticalPreferred,
                        t.Width, t.Height, t.Text.rectTransform.rect.width, t.Text.rectTransform.rect.height,
                        t.Text.preferredWidth, t.Text.preferredHeight) && ContentRectContained(t.Text, driver.Container);
                    geometry &= natural;
                    if (!sameDriver) fields |= UiVisualField.LayoutDriver;
                    else if (!natural) fields |= UiVisualField.OwnRect;
                }
                string displayNow = GeometryDisplay(t.Text, t.Policy == GeometryPolicy.ProfileChange || t.AutoSize != null, AllowsNativeSelectionColor(t.Policy));
                bool nativeSizeCurrent = t.AutoSize == null || FilterOptionAutoSizePolicy.Matches(t.AutoSize.Configuration, ReadAutoSize(t.Text));
                bool display = displayNow == t.Display && nativeSizeCurrent;
                if (!nativeSizeCurrent) { failure |= UiVisualFailure.DisplayChanged; fields |= UiVisualField.FontSize; }
                if (!display) { failure |= UiVisualFailure.DisplayChanged; fields |= ChangedDisplayFields(t.Display, displayNow); }
                bool layoutStable = true;
                if (t.Policy is GeometryPolicy.ScopedModelLabel or GeometryPolicy.ProfileOffLabel or GeometryPolicy.NativeContentFit or GeometryPolicy.ScopedPopupCaption or GeometryPolicy.FooterComposite or GeometryPolicy.ScopedHeaderPower or GeometryPolicy.OptionCheckboxComposite or GeometryPolicy.ScopedShopHeader or GeometryPolicy.ScopedCharacterEnhance or GeometryPolicy.ScopedShopHelp)
                {
                    string motion = GeometryMotion(t.Text);
                    t.MotionStable = motion == t.PreviousMotion ? t.MotionStable + 1 : 0;
                    t.PreviousMotion = motion;
                    layoutStable = t.MotionStable >= 1;
                    if (!layoutStable) fields |= UiVisualField.AncestorMotion;
                }
                if (!geometry) { failure |= UiVisualFailure.LayoutChanged; if (fields == UiVisualField.None) fields |= UiVisualField.OwnRect; }
                if (t.Policy == GeometryPolicy.FooterComposite && !CurrentFooterContainer(t))
                { FailGeometry(pair.Key, t, UiVisualFailure.OwnerRebound, UiVisualField.Parent); continue; }
                if (t.Policy == GeometryPolicy.ScopedHeaderPower && !CurrentHeaderPowerGeometry(t))
                { FailGeometry(pair.Key, t, UiVisualFailure.OwnerRebound, UiVisualField.Parent); continue; }
                if (t.Policy == GeometryPolicy.OptionCheckboxComposite && !CurrentOptionCheckboxGeometry(t))
                { FailGeometry(pair.Key, t, UiVisualFailure.OwnerRebound, UiVisualField.Parent); continue; }
                if (t.Policy == GeometryPolicy.ScopedShopHeader && !CurrentShopHeaderGeometry(t))
                { FailGeometry(pair.Key, t, UiVisualFailure.OwnerRebound, UiVisualField.Parent); continue; }
                if (t.Policy == GeometryPolicy.ScopedCharacterEnhance && !CurrentCharacterEnhanceGeometry(t))
                { FailGeometry(pair.Key, t, UiVisualFailure.OwnerRebound, UiVisualField.Parent); continue; }
                if (t.Policy == GeometryPolicy.ScopedShopHelp && !CurrentShopHelpGeometry(t))
                { FailGeometry(pair.Key,t,UiVisualFailure.OwnerRebound,UiVisualField.Parent); continue; }
                if (t.Policy == GeometryPolicy.ScopedShopHelp && !ShopHelpRegionProof(t.Text,t.ShopHelpRegion!,t.Binding,t.OwnerGeneration,true))
                { FailGeometry(pair.Key,t,UiVisualFailure.IntrinsicOverflow,UiVisualField.GlyphRect); continue; }
                if (t.Policy == GeometryPolicy.ScopedShopHeader) RecordShopHeaderGeometry(t);
                UiVisualFailure glyphFailure = GlyphFailure(t.Text, t, fonts.Medium,
                    t.Policy == GeometryPolicy.FooterComposite ? t.FooterContainer : t.Policy == GeometryPolicy.OptionCheckboxComposite ? t.OptionCheckboxContainer : t.Policy == GeometryPolicy.ScopedShopHeader ? t.ShopHeaderContainer : t.Policy == GeometryPolicy.ScopedShopHelp ? t.ShopHelpRegion : null,
                    t.Policy is GeometryPolicy.OptionCheckboxComposite or GeometryPolicy.ScopedShopHeader or GeometryPolicy.ScopedShopHelp,
                    t.Policy == GeometryPolicy.ScopedShopHeader, t.Policy == GeometryPolicy.ScopedShopHelp);
                // Recovered UI21 L3 post-KO contract: the selected native cluster may
                // resize naturally, but its complete KO must still occupy one line.
                if (t.Policy == GeometryPolicy.DecorativeCluster &&
                    !DecorativeKoGeometryPolicy.SingleLine(t.Text.textInfo?.lineCount ?? 0))
                    glyphFailure |= UiVisualFailure.IntrinsicOverflow;
                bool fits = t.Policy == GeometryPolicy.BadgeIntrinsic ? BadgeIntrinsicFit(t.Text) : glyphFailure == UiVisualFailure.None;
                if (!fits)
                {
                    failure |= t.Policy == GeometryPolicy.BadgeIntrinsic ? UiVisualFailure.IntrinsicOverflow : glyphFailure;
                    if ((failure & UiVisualFailure.IntrinsicOverflow) != 0) fields |= UiVisualField.GlyphRect;
                    if ((failure & UiVisualFailure.MaskClipped) != 0) fields |= UiVisualField.Mask;
                    if ((failure & UiVisualFailure.RenderChannelHidden) != 0) fields |= UiVisualField.Channel;
                    if ((failure & UiVisualFailure.DisplayChanged) != 0) fields |= UiVisualField.Font;
                }
                if (t.Policy == GeometryPolicy.ProfileChange)
                {
                    var sizeState = ReadGeometrySize(t);
                    bool sizeCurrent = sizeState.Active && !sizeState.DestroyedOrRebound && sizeState.Value == t.LastSize;
                    fits &= float.IsFinite(t.Text.preferredHeight) && t.Text.preferredHeight <= 24;
                    if (meshCurrent && geometry && display && sizeCurrent && !fits && t.LastSize > 23)
                    {
                        float next = t.LastSize - 1;
                        var sizeWrite = geometrySizes.TryWrite(t.SizeTarget!, "General:profile-change", sizeState, next);
                        if (sizeWrite.Written)
                        {
                            t.LastSize = next; t.Settled = 0;
                            log.Record("GENERAL_L2_CURRENT_OWNED_SIZE_TRIAL");
                            continue; // A different natural mesh frame must evaluate each trial.
                        }
                        FailGeometry(pair.Key, t, UiVisualFailure.DisplayChanged, UiVisualField.FontSize); continue;
                    }
                    display &= sizeCurrent;
                    if (!sizeCurrent) { failure |= UiVisualFailure.DisplayChanged; fields |= UiVisualField.FontSize; }
                }
                if (!fits && (failure & (UiVisualFailure.IntrinsicOverflow | UiVisualFailure.MaskClipped | UiVisualFailure.RenderChannelHidden)) == 0)
                { failure |= UiVisualFailure.IntrinsicOverflow; fields |= UiVisualField.GlyphRect; }
                var assessment = UiVisualSettlement.Assess(frame - t.WrittenFrame, failure, layoutStable);
                if (assessment.Action == UiVisualAction.Accept)
                { t.Settled++; t.PreviousCluster = cluster; if (t.Settled >= 2) RecordGeometry(t, UiVisualPhase.Settled); }
                else if (assessment.Action == UiVisualAction.Wait)
                { t.Settled = 0; RecordGeometry(t, assessment.Phase, assessment.Failure, fields); }
                else FailGeometry(pair.Key, t, assessment.Failure, fields);
            }
            catch { FailGeometry(pair.Key, t, UiVisualFailure.ReadFailed, UiVisualField.Target); }
        }
    }

    void FailGeometry(PhysicalTargetIdentity identity, GeometryTicket t, UiVisualFailure failure, UiVisualField fields)
    {
        if ((failure & UiVisualFailure.IntrinsicOverflow) != 0) RecordOptionCheckboxGeometry(t);
        RecordFilterAutoSizeFailure(t);
        t.Failed = true; t.Failure = failure; t.ChangedFields = fields;
        var key = new UiGeometryHoldKey(t.Binding.Id, Ptr(t.Text), t.OwnerGeneration, t.UiGeneration);
        const UiVisualFailure identityBoundary = UiVisualFailure.OwnerRebound | UiVisualFailure.SourceChanged | UiVisualFailure.TargetInactive | UiVisualFailure.ControlChanged;
        // A new native source/control callback revalidates identity failures. A stable visual
        // failure is held for the owner/UI epoch; Queue's timing reset is not geometry proof.
        if ((failure & ~identityBoundary) == 0)
        { if (transientGeometryRejected.Count < 256) transientGeometryRejected[key] = t.ProducerFrame; }
        else if (geometryRejected.Count < 256) geometryRejected.Add(key);
        RecordGeometry(t, UiVisualPhase.Failed, failure, fields);
        RestoreGeometryTicket(identity, "GENERAL_GEOMETRY_GUARD_FAILED");
    }
    void ForgetGeometryOwner(long generation)
    {
        geometryRejected.RemoveWhere(key => key.OwnerGeneration == generation);
        foreach (var key in transientGeometryRejected.Keys.Where(k => k.OwnerGeneration == generation).ToArray()) transientGeometryRejected.Remove(key);
        geometryDiagnostics.ForgetOwner(generation);
        ForgetPopupPreparation(generation);
    }
    bool RecordGeometry(GeometryTicket t, UiVisualPhase phase, UiVisualFailure failure = UiVisualFailure.None, UiVisualField fields = UiVisualField.None, uint detail = 0)
    {
        // All attempted tickets share one finite budget; failed/unsupported proof retries
        // cannot restart logging merely by allocating a fresh ticket or resetting Queue.Started.
        if (!geometryDiagnostics.TryRecord(new(t.Binding.Id, Ptr(t.Text), t.OwnerGeneration, t.UiGeneration), phase, failure, fields, detail)) return false;
        string key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(t.Binding.Id)))[..12];
        int age = frame - (phase is UiVisualPhase.SourceJoin or UiVisualPhase.Prepare ? t.PreparedFrame : t.WrittenFrame);
        log.Record($"GEO_{key}_O{t.OwnerGeneration}_U{t.UiGeneration}_{phase.ToString().ToUpperInvariant()}_M{(int)failure:X}_F{(int)fields:X}_A{Math.Clamp(age,0,999)}_B{(t.CallbackScopedWrite?1:0)}");
        if (detail != 0) log.Record($"GEOCL_{key}_O{t.OwnerGeneration}_U{t.UiGeneration}_{phase.ToString().ToUpperInvariant()}_D{detail:X}");
        if (phase is UiVisualPhase.Failed or UiVisualPhase.Settled)
        {
            log.Record($"GEON_{key}_O{t.OwnerGeneration}_W{t.Width100}_H{t.Height100}_V{t.VisibleGlyphs}_R{t.MaterialChannels:X}_Q{t.HiddenChannels:X}_P{(t.PrimaryHidden?1:0)}");
            static int Metric(float n) => float.IsFinite(n) ? (int)Math.Clamp(n * 100, -99999, 99999) : 0;
            int driver = t.ContentDriver == null ? 0 : (t.ContentDriver.HorizontalPreferred ? 1 : 0) | (t.ContentDriver.VerticalPreferred ? 2 : 0) |
                (t.ContentDriver.GroupHorizontal ? 4 : 0) | (t.ContentDriver.GroupVertical ? 8 : 0);
            log.Record($"GEOD_{key}_O{t.OwnerGeneration}_BW{Metric(t.Width)}_BH{Metric(t.Height)}_DW{t.Width100-Metric(t.Width)}_DH{t.Height100-Metric(t.Height)}_D{driver:X}");
            if (t.Policy == GeometryPolicy.FooterComposite)
                log.Record($"GEOF_{key}_O{t.OwnerGeneration}_LEAF{(t.FooterLeafFits?1:0)}_BUTTON{(t.FooterCompositeFits?1:0)}");
        }
        return true;
    }
    void RecordProfilePrepareDecline(Owner owner, UiBinding binding, TextMeshProUGUI text, UiVisualPhase phase, UiVisualFailure failure, UiVisualField fields, uint detail = 0)
    {
        // Only this already-supported selected-profile scope is diagnosed. Native proof is
        // neither bypassed nor changed; malformed/unsupported current state keeps source.
        if (binding.AdapterId != "ui.caption.v1" || binding.Selector.ScopeKey != "gc.labeltab" ||
            binding.Selector.SlotKey is not ("profile_tab" or "profile_style_tab" or "profile_voice_tab")) return;
        if (owner.Value is not Gc.LabelTab label || Ptr(label.onObjectText) != Ptr(text)) return;
        if (RecordGeometry(new GeometryTicket { Text = text, Binding = binding, OwnerGeneration = owner.Lifetime,
            UiGeneration = host.Snapshot.Generations.Ui, PreparedFrame = frame, WrittenFrame = frame }, phase, failure, fields, detail))
        {
            string key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(binding.Id)))[..12];
            int meshCharacters = phase is UiVisualPhase.JpBaseline or UiVisualPhase.ClusterBaseline ? Math.Clamp(text.textInfo?.characterCount??0,0,9999) : 0;
            log.Record($"GEOJ_{key}_O{owner.Lifetime}_U{host.Snapshot.Generations.Ui}_L{Math.Clamp((text.text??"").Length,0,9999)}_E{Math.Clamp(binding.SourceIdentity.ExactJp.Length,0,9999)}_S{(label.isSelecting?1:0)}_N{meshCharacters}");
        }
    }

    void RestoreGeometryTicket(PhysicalTargetIdentity identity, string reason)
    {
        // This is the only geometry failure write path: restore only the service's current
        // own text lease. No font, rect, layout, source model, or native controls are mutated.
        // Restore retry keeps the original own lease; successful release is recorded separately.
        if (!geometryOwned.TryGetValue(identity, out var ticket)) return;
        RecordGeometry(ticket, UiVisualPhase.Failed, ticket.Failure, ticket.ChangedFields);
        bool sizeDone = RestoreGeometrySize(identity);
        var result = RestoreGeometryText(identity, ticket);
        if (sizeDone && result is not (RestoreOutcome.RetryableReadFailure or RestoreOutcome.RetryableWriteFailure))
        {
            RecordGeometryRestoreOutcome(ticket, result);
            geometryOwned.Remove(identity);
        }
    }
    RestoreOutcome RestoreGeometryText(PhysicalTargetIdentity identity, GeometryTicket? ticket)
    {
        if (ticket == null) return ownership.RestoreTargetDetailed(identity);
        ticket.RestoringText = true;
        try { return ownership.RestoreTargetDetailed(identity); }
        finally { ticket.RestoringText = false; }
    }
    readonly HashSet<string> geometryReleaseReports = new(StringComparer.Ordinal);
    void RecordGeometryRestoreOutcome(GeometryTicket ticket, RestoreOutcome result)
    {
        if (result == RestoreOutcome.Restored)
        { RecordGeometry(ticket, UiVisualPhase.Restored, ticket.Failure, ticket.ChangedFields); return; }
        string id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ticket.Binding.Id)))[..12];
        string code = $"GEOR_{id}_O{ticket.OwnerGeneration}_U{ticket.UiGeneration}_{result.ToString().ToUpperInvariant()}";
        if (geometryReleaseReports.Count < 1024 && geometryReleaseReports.Add(code)) log.Record(code);
    }
    bool RestoreGeometrySize(PhysicalTargetIdentity identity)
    {
        if (!geometryOwned.TryGetValue(identity, out var ticket) || ticket.SizeTarget == null) return true;
        var result = geometrySizes.Restore(identity, "fontSize");
        return result is not (RestoreOutcome.RetryableReadFailure or RestoreOutcome.RetryableWriteFailure);
    }
    bool RestoreGeometryTarget(PhysicalTargetIdentity identity)
    {
        gate.Require();
        if (!RestoreGeometrySize(identity)) return false;
        if (geometryOwned.TryGetValue(identity, out var ticket))
        {
            var result = RestoreGeometryText(identity, ticket);
            if (result is RestoreOutcome.RetryableReadFailure or RestoreOutcome.RetryableWriteFailure) return false;
            RecordGeometryRestoreOutcome(ticket, result);
        }
        geometryOwned.Remove(identity);
        return true;
    }
    bool RestoreGeometry()
    {
        gate.Require();
        jpGeometry.Clear();
        foreach (var pair in geometryOwned.ToArray())
        {
            pair.Value.Failed = true;
            RestoreGeometryTicket(pair.Key, "GENERAL_GEOMETRY_RESTORE");
        }
        if (geometryOwned.Count == 0) { geometryRejected.Clear(); transientGeometryRejected.Clear(); }
        return geometryOwned.Count == 0;
    }

    sealed class GeometryFloatTarget(GameThreadGate accessGate, TextMeshProUGUI text, Func<FloatTargetState> read) : IUiFloatTarget, IUiTargetAccessGate
    {
        public bool CanAccess => accessGate.CanAccess;
        public FloatTargetState ReadState() { accessGate.Require(); return read(); }
        public bool TryWriteFloat(FloatTargetState expected, float value)
        {
            accessGate.Require();
            if (ReadState() != expected || !expected.Active || expected.DestroyedOrRebound) return false;
            text.fontSize = value;
            return true; // A returned setter owns the write; later settlement reads retain the lease on failure.
        }
    }
    FloatTargetState ReadGeometrySize(GeometryTicket ticket)
    {
        gate.Require();
        var text = ticket.Text; var owner = ticket.Profile; var identity = ticket.Identity!;
        bool destroyed = !Alive(text) || !Alive(owner);
        bool rebound = !destroyed && (text.GetInstanceID() != ticket.Instance || owner!.GetInstanceID() != ticket.ProfileInstance ||
            Ptr(owner._userUnit) != ticket.ProfileModel || Ptr(owner._change) != ticket.ProfileChange || text.gameObject.scene.handle != ticket.Scene);
        if (owner == null || destroyed || rebound)
            return new(identity, "fontSize", ticket.LastSize, identity.Lifetime, identity.Lifetime, "", false, true);
        bool current = text.enabled && text.gameObject.activeInHierarchy && owner!.gameObject.activeInHierarchy &&
            Alive(owner._change) && text.transform.IsChildOf(owner._change.transform) &&
            (text.text == ticket.WrittenText || text.text == ticket.Binding.SourceIdentity.ExactJp);
        string protectedIdentity = JsonSerializer.Serialize(new
        {
            owner = Ptr(owner), instance = ticket.ProfileInstance, model = Ptr(owner._userUnit), change = Ptr(owner._change),
            display = GeometryDisplay(text, true), layout = GeometryLayout(text, GeometryPolicy.ProfileChange)
        });
        return new(identity, "fontSize", text.fontSize, identity.Lifetime, identity.Lifetime, protectedIdentity, current);
    }

    static string GeometryDisplay(TextMeshProUGUI t, bool omitSize = false, bool allowSelectionColor = false) => JsonSerializer.Serialize(new
    {
        font = Ptr(t.font), material = Ptr(t.fontSharedMaterial), size = omitSize ? (float?)null : t.fontSize, t.fontSizeMin, t.fontSizeMax,
        t.enableAutoSizing, t.maxVisibleCharacters, t.overflowMode, t.richText, t.raycastTarget,
        scale = new[] { t.transform.localScale.x, t.transform.localScale.y, t.transform.localScale.z },
        // InertToggle owns selection/hover colors; the translation writer never owns that state.
        color = allowSelectionColor ? null : new[] { t.color.r, t.color.g, t.color.b, t.color.a },
        margin = new[] { t.margin.x, t.margin.y, t.margin.z, t.margin.w }
    });
    static bool AllowsNativeSelectionColor(GeometryPolicy policy) => policy is GeometryPolicy.ScopedModelLabel or GeometryPolicy.ProfileOffLabel or GeometryPolicy.FooterComposite;
    static ContentFitDriver? CaptureContentFitDriver(TextMeshProUGUI text)
    {
        if (!Alive(text) || !Alive(text.transform.parent)) return null;
        var fitter = text.GetComponent<ContentSizeFitter>(); var group = text.transform.parent.GetComponent<HorizontalOrVerticalLayoutGroup>();
        var element = text.GetComponent<LayoutElement>(); var ratio = text.GetComponent<AspectRatioFitter>();
        // Only the actual standard preferred-size producer, not data or a guessed size rule.
        if (Alive(ratio) && ratio.enabled || Alive(element) && element.enabled &&
            (element.ignoreLayout || element.minWidth > 0 || element.minHeight > 0 || element.preferredWidth >= 0 || element.preferredHeight >= 0 || element.flexibleWidth > 0 || element.flexibleHeight > 0)) return null;
        bool h = Alive(fitter) && fitter.enabled && fitter.horizontalFit == ContentSizeFitter.FitMode.PreferredSize;
        bool v = Alive(fitter) && fitter.enabled && fitter.verticalFit == ContentSizeFitter.FitMode.PreferredSize;
        bool gh = Alive(group) && group.enabled && group.childControlWidth && !group.childForceExpandWidth && !group.childScaleWidth;
        bool gv = Alive(group) && group.enabled && group.childControlHeight && !group.childForceExpandHeight && !group.childScaleHeight;
        if (!(h || v || gh || gv)) return null;
        var button = Parent<Button>(text); var container = Alive(button) ? button!.GetComponent<RectTransform>() : null;
        if (container is null || !Alive(container) || !text.transform.IsChildOf(container)) return null;
        return new(Alive(fitter) ? fitter.GetInstanceID() : 0, Alive(group) ? group.GetInstanceID() : 0, container.GetInstanceID(), h || gh, v || gv, gh, gv, container);
    }
    static bool ContentRectContained(TextMeshProUGUI text, RectTransform container)
    {
        if (!Alive(container) || !text.transform.IsChildOf(container)) return false;
        var r = text.rectTransform.rect;
        return new[] { new Vector3(r.xMin,r.yMin,0), new Vector3(r.xMin,r.yMax,0), new Vector3(r.xMax,r.yMax,0), new Vector3(r.xMax,r.yMin,0) }
            .All(q => Within(container.InverseTransformPoint(text.transform.TransformPoint(q)), container.rect));
    }
    static UiVisualField ChangedDisplayFields(string before, string after)
    {
        using var a = JsonDocument.Parse(before); using var b = JsonDocument.Parse(after);
        UiVisualField result = UiVisualField.None;
        foreach (var property in a.RootElement.EnumerateObject())
            if (!b.RootElement.TryGetProperty(property.Name, out var value) || property.Value.GetRawText() != value.GetRawText())
                result |= property.Name switch
                { "font" => UiVisualField.Font, "material" => UiVisualField.Material, "size" or "fontSizeMin" or "fontSizeMax" => UiVisualField.FontSize,
                  "color" => UiVisualField.Color, _ => UiVisualField.Display };
        return result;
    }
    static string GeometryMotion(TextMeshProUGUI text)
    {
        var ancestors = new List<object>();
        for (var n = text.transform.parent; Alive(n) && ancestors.Count < 6; n = n.parent)
        {
            var r = n.GetComponent<RectTransform>(); var group = n.GetComponent<CanvasGroup>();
            if (Alive(r)) ancestors.Add(new { rect = NumericRect(r), alpha = Alive(group) ? group.alpha : 1f });
        }
        return JsonSerializer.Serialize(ancestors);
    }
    static object NumericRect(RectTransform r) => new
    {
        pointer = Ptr(r), instance = r.GetInstanceID(), rect = new[] { r.rect.x, r.rect.y, r.rect.width, r.rect.height },
        pivot = new[] { r.pivot.x, r.pivot.y }, anchors = new[] { r.anchorMin.x, r.anchorMin.y, r.anchorMax.x, r.anchorMax.y },
        anchored = new[] { r.anchoredPosition.x, r.anchoredPosition.y },
        scale = new[] { r.localScale.x, r.localScale.y, r.localScale.z },
        rotation = new[] { r.localRotation.x, r.localRotation.y, r.localRotation.z, r.localRotation.w }
    };
    static string NumericRectInvariant(RectTransform r, bool allowHorizontal = false, bool allowVertical = false, bool? allowX = null, bool? allowY = null) => JsonSerializer.Serialize(new
    {
        instance = r.GetInstanceID(), pivot = new[] { r.pivot.x, r.pivot.y },
        anchors = new[] { r.anchorMin.x, r.anchorMin.y, r.anchorMax.x, r.anchorMax.y },
        scale = new[] { r.localScale.x, r.localScale.y, r.localScale.z }, height = allowVertical ? (float?)null : r.rect.height,
        y = (allowY ?? allowVertical) ? (float?)null : r.anchoredPosition.y, width = allowHorizontal ? (float?)null : r.rect.width,
        x = (allowX ?? allowHorizontal) ? (float?)null : r.anchoredPosition.x,
        rotation = new[] { r.localRotation.x, r.localRotation.y, r.localRotation.z, r.localRotation.w }
    });
    static string GeometryLayout(TextMeshProUGUI t, GeometryPolicy policy)
    {
        var parent = t.transform.parent;
        Require(Alive(parent) && parent.childCount <= 64, "GEOMETRY_SIBLING_BOUND");
        if (policy is GeometryPolicy.ScopedModelLabel or GeometryPolicy.ProfileOffLabel or GeometryPolicy.NativeContentFit or GeometryPolicy.ScopedPopupCaption or GeometryPolicy.FooterComposite or GeometryPolicy.ScopedHeaderPower or GeometryPolicy.OptionCheckboxComposite or GeometryPolicy.ScopedShopHeader or GeometryPolicy.ScopedCharacterEnhance or GeometryPolicy.ScopedShopHelp)
        {
            // Existing current-model labels preserve their own rect and layout drivers. Popup
            // ancestors and toggle highlight are game-owned animation, measured separately above.
            var scopedFitter = t.GetComponent<ContentSizeFitter>(); var scopedGroup = parent.GetComponent<HorizontalOrVerticalLayoutGroup>();
            var driver = policy == GeometryPolicy.NativeContentFit ? CaptureContentFitDriver(t) : null;
            return JsonSerializer.Serialize(new
            {
                parent = parent.GetInstanceID(), own = NumericRectInvariant(t.rectTransform, driver?.HorizontalPreferred == true, driver?.VerticalPreferred == true,
                    driver?.GroupHorizontal == true, driver?.GroupVertical == true),
                ownPosition = policy == GeometryPolicy.NativeContentFit ? null : new[] { t.rectTransform.anchoredPosition.x, t.rectTransform.anchoredPosition.y },
                contentDriver = driver == null ? null : new { driver.FitterInstance, driver.GroupInstance, driver.HorizontalPreferred, driver.VerticalPreferred, driver.GroupHorizontal, driver.GroupVertical },
                container = driver != null ? NumericRectInvariant(driver.Container) : policy == GeometryPolicy.FooterComposite
                    ? NumericRectInvariant(Parent<InertButton>(t)!.GetComponent<RectTransform>()) : policy == GeometryPolicy.ScopedHeaderPower
                    ? NumericRectInvariant(Parent<Gc.HeaderTopController>(t)!._powerButton.GetComponent<RectTransform>()) : policy == GeometryPolicy.OptionCheckboxComposite
                    ? NumericRectInvariant(Parent<Gc.Option.OptionPopup>(t)!.soundAndTextOptionArea.messageSpeedOption.checkBox.GetComponent<RectTransform>()) : policy == GeometryPolicy.ScopedShopHeader
                    ? NumericRectInvariant(ShopHeaderContainer(t)!) : policy == GeometryPolicy.ScopedCharacterEnhance
                    ? NumericRectInvariant(CharacterEnhanceContainer(t)!) : policy == GeometryPolicy.ScopedShopHelp
                    ? NumericRectInvariant(t.transform.parent.GetComponent<RectTransform>()) : null,
                fitter = Alive(scopedFitter) ? new { instance = scopedFitter.GetInstanceID(), scopedFitter.enabled, h = (int)scopedFitter.horizontalFit, v = (int)scopedFitter.verticalFit } : null,
                group = Alive(scopedGroup) ? new { instance = scopedGroup.GetInstanceID(), scopedGroup.enabled, scopedGroup.childControlWidth, scopedGroup.childControlHeight,
                    scopedGroup.childForceExpandWidth, scopedGroup.childForceExpandHeight, scopedGroup.spacing, scopedGroup.reverseArrangement,
                    scopedGroup.childScaleWidth, scopedGroup.childScaleHeight, alignment = (int)scopedGroup.childAlignment,
                    padding = new[] { scopedGroup.padding.left, scopedGroup.padding.right, scopedGroup.padding.top, scopedGroup.padding.bottom } } : null
            });
        }
        if (policy == GeometryPolicy.BadgeIntrinsic)
        {
            var cell = Parent<Gc.Shop.ExchangeShopScrollCell>(t);
            Require(Alive(cell), "BADGE_CELL_REQUIRED");
            var local = new List<object>();
            for (var n = t.transform; Alive(n); n = n.parent)
            {
                Require(local.Count < 12, "BADGE_LOCAL_LAYOUT_BOUND");
                var rect = n.GetComponent<RectTransform>();
                if (Alive(rect)) local.Add(NumericRect(rect));
                if (Ptr(n) == Ptr(cell!.transform)) return JsonSerializer.Serialize(local);
            }
            throw new InvalidDataException("BADGE_LOCAL_CELL_NOT_REACHED");
        }
        var siblings = new List<object>();
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i); var r = child.GetComponent<RectTransform>();
            if (Alive(r) && Ptr(child) != Ptr(t.transform)) siblings.Add(NumericRect(r));
        }
        var ancestors = new List<object>();
        for (var n = parent; Alive(n) && ancestors.Count < 6; n = n.parent)
        {
            var r = n.GetComponent<RectTransform>();
            if (Alive(r)) ancestors.Add(NumericRect(r));
        }
        var fitter = t.GetComponent<ContentSizeFitter>(); var group = parent.GetComponent<HorizontalOrVerticalLayoutGroup>();
        return JsonSerializer.Serialize(new
        {
            own = NumericRectInvariant(t.rectTransform, policy == GeometryPolicy.NaturalWidth),
            position = new[] { t.rectTransform.anchoredPosition.x, t.rectTransform.anchoredPosition.y }, siblings, ancestors,
            fitter = Alive(fitter) ? new { instance = fitter.GetInstanceID(), fitter.enabled, h = (int)fitter.horizontalFit, v = (int)fitter.verticalFit } : null,
            group = Alive(group) ? new { instance = group.GetInstanceID(), group.enabled, group.childControlWidth, group.childControlHeight,
                group.childForceExpandWidth, group.childForceExpandHeight, group.childScaleWidth, group.childScaleHeight,
                group.reverseArrangement, group.spacing, alignment = (int)group.childAlignment,
                padding = new[] { group.padding.left, group.padding.right, group.padding.top, group.padding.bottom } } : null
        });
    }
    static bool HasNaturalWidthDriver(TextMeshProUGUI t)
    {
        var fitter = t.GetComponent<ContentSizeFitter>();
        var group = t.transform.parent.GetComponent<HorizontalOrVerticalLayoutGroup>();
        return Alive(fitter) && fitter.enabled && fitter.horizontalFit == ContentSizeFitter.FitMode.PreferredSize ||
            Alive(group) && group.enabled && group.childControlWidth;
    }
    static string MeshSource(TextMeshProUGUI text, string value)
    {
        Require(value.Length <= 4096, "GEOMETRY_TEXT_BOUND");
        return text.richText ? Regex.Replace(value, "<[^<>]*>", "", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(30)) : value;
    }
    static string RenderedGeometryText(TextMeshProUGUI t)
    {
        var info = t.textInfo;
        if (info?.characterInfo == null || info.characterCount <= 0 || info.characterCount > 4096 || info.characterCount > info.characterInfo.Length) return "";
        return string.Concat(info.characterInfo.Take(info.characterCount).Select(c => ((char)c.character).ToString()));
    }
    static bool Within(Vector3 q, Rect rect) => float.IsFinite(q.x) && float.IsFinite(q.y) &&
        q.x >= rect.xMin - .05f && q.x <= rect.xMax + .05f && q.y >= rect.yMin - .05f && q.y <= rect.yMax + .05f;
    static IEnumerable<Vector3> GlyphCorners(TMP_CharacterInfo c) => new[]
    {
        new Vector3(c.bottomLeft.x,c.bottomLeft.y,0), new Vector3(c.bottomLeft.x,c.topRight.y,0),
        new Vector3(c.topRight.x,c.topRight.y,0), new Vector3(c.topRight.x,c.bottomLeft.y,0)
    };
    static bool GlyphContained(TextMeshProUGUI t) => GlyphFailure(t) == UiVisualFailure.None;
    static UiVisualFailure OriginalJpCompositeGlyphFailure(TextMeshProUGUI text, ClusterCapture cluster, out uint detail)
    {
        detail = (text.isTextTruncated ? 1u : 0u) | (text.firstOverflowCharacterIndex >= 0 ? 2u : 0u);
        // The repair changes only original JP leaf-vs-composite containment. An actual
        // truncation/overflow signal is still a hold; no cull/mesh/property is forced.
        if ((detail & 3) != 0) return UiVisualFailure.IntrinsicOverflow;
        var info = text.textInfo; var parent = text.transform.parent; var composite = Alive(parent) ? parent.GetComponent<RectTransform>() : null;
        if (info?.characterInfo == null || info.characterCount <= 0 || info.characterCount > 4096 || info.characterCount > info.characterInfo.Length ||
            !Alive(composite) || !ClusterBaseline(cluster)) return UiVisualFailure.LayoutChanged;
        var all = info.characterInfo.Take(info.characterCount).ToArray(); var visible = all.Where(c => c.isVisible).ToArray();
        bool full = RenderedGeometryText(text) == MeshSource(text, text.text) && !text.m_havePropertiesChanged && !text.m_isLayoutDirty;
        bool every = visible.Length > 0 && all.All(c => c.isVisible || c.character <= char.MaxValue && char.IsWhiteSpace((char)c.character));
        bool contained = visible.All(c => GlyphCorners(c).All(q => Within(composite!.InverseTransformPoint(text.transform.TransformPoint(q)), composite.rect)));
        if (visible.Any(c => GlyphCorners(c).Any(q => !Within(q, text.rectTransform.rect)))) detail |= 4;
        bool channels = visible.All(c => GlyphChannelVisible(text, c)); bool masks = true; int ancestors = 0;
        for (var n = text.transform; Alive(n); n = n.parent)
        {
            if (++ancestors > 32) { masks = false; break; }
            var mask = n.GetComponent<RectMask2D>(); var rect = n.GetComponent<RectTransform>();
            if (!Alive(mask) || !mask.enabled) continue;
            if (!Alive(rect) || visible.Any(c => GlyphCorners(c).Any(q => !Within(rect.InverseTransformPoint(text.transform.TransformPoint(q)), rect.rect))))
                masks = false;
        }
        if (DecorativeJpGeometryPolicy.Eligible(full, ClusterBaseline(cluster), every, contained, masks, channels)) return UiVisualFailure.None;
        if (!full) return UiVisualFailure.MeshStale;
        if (!every) { detail |= 8; return UiVisualFailure.IntrinsicOverflow; }
        if (!contained) { detail |= 16; return UiVisualFailure.IntrinsicOverflow; }
        if (!masks) { detail |= 32; return UiVisualFailure.MaskClipped; }
        detail |= 64; return UiVisualFailure.RenderChannelHidden;
    }
    static UiVisualFailure GlyphFailure(TextMeshProUGUI t, GeometryTicket? diagnostic = null, TMP_FontAsset? approvedMedium = null, RectTransform? visibleContainer = null, bool fullComposite = false, bool shopMaterial = false, bool shopHelpMaterial = false)
    {
        var info = t.textInfo;
        if (info?.characterInfo == null || info.characterCount <= 0 || info.characterCount > 4096 || info.characterCount > info.characterInfo.Length)
            return UiVisualFailure.MeshStale;
        if (t.isTextTruncated || t.firstOverflowCharacterIndex >= 0) return UiVisualFailure.IntrinsicOverflow;
        var chars = info.characterInfo.Take(info.characterCount).Where(c => c.isVisible).ToArray();
        if (diagnostic != null)
        {
            static int Metric(float n) => float.IsFinite(n) ? (int)Math.Clamp(n * 100, -99999, 99999) : 0;
            diagnostic.Width100 = Metric(t.rectTransform.rect.width); diagnostic.Height100 = Metric(t.rectTransform.rect.height);
            diagnostic.VisibleGlyphs = chars.Length; diagnostic.MaterialChannels = diagnostic.HiddenChannels = 0;
            diagnostic.PrimaryHidden = !Alive(t.canvasRenderer) || t.canvasRenderer.cull || t.canvasRenderer.GetAlpha() <= 0;
            foreach (var c in chars)
            {
                int index = c.materialReferenceIndex;
                if (index is >= 0 and <= 8)
                { diagnostic.MaterialChannels |= 1 << index; if (!GlyphChannelVisible(t, c)) diagnostic.HiddenChannels |= 1 << index; }
            }
        }
        bool insideLeaf = chars.Length > 0 && chars.All(c => GlyphCorners(c).All(q => Within(q, t.rectTransform.rect)));
        if (fullComposite && chars.Any(c => shopHelpMaterial ? !ShopHelpPlainMaterial(t,c) : shopMaterial ? !ShopHeaderPlainMaterial(t, c) : !OptionCheckboxPlainMaterial(t, c))) return UiVisualFailure.DisplayChanged;
        bool insideContainer = visibleContainer != null && chars.Length > 0 && chars.All(c => (fullComposite ? GlyphCorners(c) : FooterVisibleCorners(t,c)).All(q =>
            Within(visibleContainer.InverseTransformPoint(t.transform.TransformPoint(q)), visibleContainer.rect)));
        if (diagnostic?.Policy == GeometryPolicy.FooterComposite)
        {
            diagnostic.FooterLeafFits = insideLeaf;
            diagnostic.FooterCompositeFits = insideContainer;
        }
        bool contained = fullComposite ? insideContainer : GeneralUiGlyphBoundsPolicy.Contains(visibleContainer != null, insideLeaf, insideContainer);
        if (chars.Length == 0 || !contained) return UiVisualFailure.IntrinsicOverflow;
        if (approvedMedium != null && chars.Any(c => c.character is >= '\uAC00' and <= '\uD7A3' or >= '\u3130' and <= '\u318F' &&
                (!Alive(c.fontAsset) || Ptr(c.fontAsset) != Ptr(approvedMedium)))) return UiVisualFailure.DisplayChanged;
        // A Medium fallback glyph is rendered by its own TMP_SubMeshUI. A culled/empty
        // primary mesh is not evidence that those glyphs are hidden. Never change cull.
        if (chars.Any(c => !GlyphChannelVisible(t, c))) return UiVisualFailure.RenderChannelHidden;
        int ancestors = 0;
        for (var n = t.transform; Alive(n); n = n.parent)
        {
            if (++ancestors > 32) return UiVisualFailure.MaskClipped;
            var mask = n.GetComponent<RectMask2D>(); var rect = n.GetComponent<RectTransform>();
            if (!Alive(mask) || !mask.enabled) continue;
            if (!Alive(rect) || chars.Any(c => GlyphCorners(c).Any(q => !Within(rect.InverseTransformPoint(t.transform.TransformPoint(q)), rect.rect))))
                return UiVisualFailure.MaskClipped;
        }
        return UiVisualFailure.None;
    }
    static bool GlyphChannelVisible(TextMeshProUGUI text, TMP_CharacterInfo character)
    {
        int index = character.materialReferenceIndex;
        CanvasRenderer? renderer;
        if (index == 0)
        {
            if (!Alive(character.fontAsset) || Ptr(character.fontAsset) != Ptr(text.font)) return false;
            renderer = text.canvasRenderer;
        }
        else
        {
            var subs = text.m_subTextObjects;
            if (index < 0 || index > 8 || subs == null || index >= subs.Length) return false;
            var sub = subs[index];
            if (!Alive(sub) || !sub.enabled || !sub.gameObject.activeInHierarchy || Ptr(sub.m_TextComponent) != Ptr(text) ||
                !Alive(character.fontAsset) || Ptr(sub.fontAsset) != Ptr(character.fontAsset)) return false;
            renderer = sub.m_CanvasRenderer;
        }
        return UiGlyphChannelPolicy.Visible(index, true, true, Alive(renderer), Alive(renderer) && renderer!.cull,
            Alive(renderer) ? renderer!.GetAlpha() : 0);
    }
    static bool BadgeIntrinsicFit(TextMeshProUGUI t)
    {
        // Viewport clipping is measured separately. It must never masquerade as intrinsic
        // text overflow on a partially visible recycled cell.
        var cell = Parent<Gc.Shop.ExchangeShopScrollCell>(t); var view = Parent<Gc.Shop.ExchangeShopProductsView>(t);
        var scroll = view?._scrollView; var sr = scroll?._scrollRect;
        if (cell == null || view == null || scroll == null || sr == null || !Alive(cell) || !Alive(view) || !Alive(cell._exchangeBadge) || !t.transform.IsChildOf(cell._exchangeBadge.transform) ||
            !Alive(scroll) || !Alive(sr)) return false;
        var content = sr.content;
        var maskParent = Alive(content) ? content.parent : null;
        var clipMask = Alive(maskParent) ? maskParent!.GetComponent<RectMask2D>() : null;
        var viewport = Alive(clipMask) ? clipMask!.GetComponent<RectTransform>() : null;
        var declared = sr.viewport; var badge = cell._exchangeBadge.GetComponent<RectTransform>();
        bool related = Alive(viewport) && Alive(declared) &&
            (Ptr(viewport) == Ptr(declared) || viewport!.IsChildOf(declared) || declared!.IsChildOf(viewport)) &&
            (viewport!.IsChildOf(sr.transform) || sr.transform.IsChildOf(viewport));
        if (!related || !Alive(clipMask) || !clipMask!.enabled || !Alive(content) || !Alive(badge) ||
            Ptr(content) != Ptr(scroll._content) || !cell.transform.IsChildOf(content) || !content.IsChildOf(viewport)) return false;
        var info = t.textInfo;
        if (info?.characterInfo == null || info.characterCount <= 0 || info.characterCount > 4096 || info.characterCount > info.characterInfo.Length ||
            info.lineCount != 1 || t.isTextTruncated || t.firstOverflowCharacterIndex >= 0 || t.canvasRenderer.GetAlpha() <= 0) return false;
        var chars = info.characterInfo.Take(info.characterCount).Where(c => c.isVisible).ToArray();
        if (chars.Length == 0) return false;
        bool full = true; int ancestors = 0;
        foreach (var c in chars) foreach (var q in GlyphCorners(c))
        {
            if (!Within(q, t.rectTransform.rect) || !Within(badge!.InverseTransformPoint(t.transform.TransformPoint(q)), badge.rect)) return false;
            full &= Within(viewport!.InverseTransformPoint(t.transform.TransformPoint(q)), viewport.rect);
        }
        for (var n = t.transform; Alive(n); n = n.parent)
        {
            if (++ancestors > 32 || n.localRotation.x != 0 || n.localRotation.y != 0 || n.localRotation.z != 0 ||
                !float.IsFinite(n.localScale.x) || n.localScale.x <= 0 || Math.Abs(n.localScale.x - n.localScale.y) > .000001f) return false;
            var mask = n.GetComponent<RectMask2D>(); var rect = n.GetComponent<RectTransform>();
            if (!Alive(mask) || !mask.enabled || Ptr(mask) == Ptr(clipMask)) continue;
            if (!Alive(rect) || chars.Any(c => GlyphCorners(c).Any(q => !Within(rect.InverseTransformPoint(t.transform.TransformPoint(q)), rect.rect)))) return false;
        }
        return !full || !t.canvasRenderer.cull;
    }

    sealed class ClusterChild
    {
        public int Instance; public bool Label, Input, MaskContained; public float Min, Preferred, Flexible, Width, Center, Height, Pivot;
        public string Invariant = "", Driver = ""; public float[] Corners = [];
    }
    sealed class ClusterCapture
    {
        public bool Supported, Prediction, Contained, NonOverlapping, Glyphs; public float Width, Height;
        public string Driver = "", Outer = "", Inputs = ""; public ClusterChild[] Children = [];
        public uint ProofFailure;
    }
    static float[] RectCorners(RectTransform child, RectTransform parent)
    {
        var rect = child.rect;
        return new[] { new Vector3(rect.xMin, rect.yMin, 0), new Vector3(rect.xMin, rect.yMax, 0),
            new Vector3(rect.xMax, rect.yMax, 0), new Vector3(rect.xMax, rect.yMin, 0) }
            .Select(p => parent.InverseTransformPoint(child.TransformPoint(p))).SelectMany(p => new[] { p.x, p.y }).ToArray();
    }
    static ClusterCapture CaptureCluster(TextMeshProUGUI text, bool composite)
    {
        var capture = new ClusterCapture(); var parent = text.transform.parent;
        Require(Alive(parent) && parent.childCount <= 16, "L3_CHILD_BOUND");
        var rect = parent.GetComponent<RectTransform>(); var group = parent.GetComponent<HorizontalLayoutGroup>();
        if (!Alive(rect) || !Alive(group) || !group.enabled) { capture.ProofFailure = 1; return capture; }
        capture.Width = rect.rect.width; capture.Height = rect.rect.height;
        var fitter = text.GetComponent<ContentSizeFitter>();
        capture.Driver = JsonSerializer.Serialize(new
        {
            instance = group.GetInstanceID(), group.enabled, group.childControlWidth, group.childControlHeight,
            group.childForceExpandWidth, group.childForceExpandHeight, group.childScaleWidth, group.childScaleHeight,
            group.reverseArrangement, group.spacing, alignment = (int)group.childAlignment,
            padding = new[] { group.padding.left, group.padding.right, group.padding.top, group.padding.bottom },
            fitter = Alive(fitter) ? new { instance = fitter.GetInstanceID(), fitter.enabled, h = (int)fitter.horizontalFit, v = (int)fitter.verticalFit } : null
        });
        var children = new List<ClusterChild>(); bool supportedDrivers = !Alive(parent.GetComponent<Animator>());
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i); if (!child.gameObject.activeInHierarchy) continue;
            var cr = child.GetComponent<RectTransform>(); var le = child.GetComponent<LayoutElement>();
            if (!Alive(cr) || Alive(le) && le.ignoreLayout) continue;
            bool label = Ptr(child) == Ptr(text.transform);
            var ratio = child.GetComponent<AspectRatioFitter>(); var animation = child.GetComponent<Animator>();
            var components = child.GetComponents<Component>(); Require(components.Length <= 16, "L3_COMPONENT_BOUND");
            bool driver = !Alive(ratio) && !Alive(animation) &&
                (!Alive(le) || !le.enabled || !le.ignoreLayout && (!label || le.minWidth <= 0 && le.preferredWidth < 0 && le.flexibleWidth < 0 && le.layoutPriority == 1)) &&
                (label ? child.localScale.x == 1 : Math.Abs(child.localScale.x) == 1) && child.localScale.y == 1 &&
                child.localRotation.x == 0 && child.localRotation.y == 0 && child.localRotation.z == 0;
            supportedDrivers &= driver;
            var corners = RectCorners(cr, rect); bool maskContained = true; int ancestry = 0;
            for (var n = parent; Alive(n); n = n.parent)
            {
                Require(++ancestry <= 32, "L3_MASK_ANCESTOR_BOUND");
                var mask = n.GetComponent<RectMask2D>(); var mr = n.GetComponent<RectTransform>();
                if (!Alive(mask) || !mask.enabled) continue;
                if (!Alive(mr)) { maskContained = false; continue; }
                var mc = RectCorners(cr, mr);
                maskContained &= Enumerable.Range(0, 4).All(j => Within(new Vector3(mc[j * 2], mc[j * 2 + 1], 0), mr.rect));
            }
            float low = corners.Where((_, j) => j % 2 == 0).Min(), high = corners.Where((_, j) => j % 2 == 0).Max();
            children.Add(new ClusterChild
            {
                Instance = cr.GetInstanceID(), Label = label, Input = Alive(child.GetComponent<Selectable>()) || Alive(child.GetComponent<TMP_InputField>()),
                Min = LayoutUtility.GetMinWidth(cr), Preferred = LayoutUtility.GetPreferredWidth(cr), Flexible = LayoutUtility.GetFlexibleWidth(cr),
                Width = cr.rect.width, Height = cr.rect.height, Center = (low + high) * .5f - rect.rect.xMin, Pivot = cr.pivot.x,
                Corners = corners, MaskContained = maskContained, Invariant = NumericRectInvariant(cr, true),
                Driver = JsonSerializer.Serialize(new { pointer = Ptr(child), instance = cr.GetInstanceID(), label,
                    classes = components.Select(UnityNative.Class).ToArray(), aspect = Alive(ratio), animation = Alive(animation),
                    layout = Alive(le) ? new { pointer = Ptr(le), le.enabled, le.ignoreLayout, le.layoutPriority, le.minWidth, le.preferredWidth, le.flexibleWidth } : null,
                    scale = new[] { child.localScale.x, child.localScale.y, child.localScale.z },
                    rotation = new[] { child.localRotation.x, child.localRotation.y, child.localRotation.z, child.localRotation.w } })
            });
        }
        capture.Children = children.ToArray(); capture.Inputs = JsonSerializer.Serialize(children.Select(c => c.Driver).ToArray());
        var outer = new List<object>();
        for (var n = parent; Alive(n) && outer.Count < 6; n = n.parent)
        {
            var ar = n.GetComponent<RectTransform>(); if (!Alive(ar)) continue;
            outer.Add(new { rect = NumericRectInvariant(ar), mask = n.GetComponent<RectMask2D>()?.enabled,
                buttons = n.GetComponents<Button>().Select(b => new { pointer = Ptr(b), b.interactable, callback = Ptr(b.onClick) }).ToArray() });
        }
        var tab = Parent<Gc.LabelTab>(text); var profile = Alive(tab) ? Parent<Gc.UnitScene.UnitProfileController>(tab!) : null;
        if (Alive(profile) && Alive(profile!._tab))
        {
            var turner = profile._tab._tabController?.PageTurner;
            if (turner == null || !Alive(turner) || turner.tabs == null || turner.tabs.Count > 8) throw new InvalidDataException("L3_NATIVE_TAB_COLLECTION_BOUND");
            outer.Add(new { otherTabs = turner!.tabs.ToArray().Where(t => Ptr(t) != Ptr(tab)).Select(t => new
                { instance = t.GetInstanceID(), rect = NumericRectInvariant(t.GetComponent<RectTransform>()), button = Ptr(t.button), callback = Ptr(t.button?.onClick) }).ToArray() });
        }
        capture.Outer = JsonSerializer.Serialize(outer);
        capture.Supported = children.Count == 3 && children.Count(c => c.Label) == 1 && children.All(c => !c.Input) &&
            group.childControlWidth && !group.childForceExpandWidth && !group.childScaleWidth && !group.reverseArrangement && supportedDrivers;
        if (composite) capture.Supported &= Alive(fitter) && fitter.enabled &&
            fitter.horizontalFit == ContentSizeFitter.FitMode.PreferredSize && fitter.verticalFit == ContentSizeFitter.FitMode.Unconstrained;
        if (children.Count != 3 || children.Count(c => c.Label) != 1) capture.ProofFailure |= 2;
        if (children.Any(c => c.Input)) capture.ProofFailure |= 4;
        if (!group.childControlWidth || group.childForceExpandWidth || group.childScaleWidth || group.reverseArrangement || !supportedDrivers) capture.ProofFailure |= 8;
        if (composite && (!Alive(fitter) || !fitter.enabled || fitter.horizontalFit != ContentSizeFitter.FitMode.PreferredSize || fitter.verticalFit != ContentSizeFitter.FitMode.Unconstrained)) capture.ProofFailure |= 16;
        if (!capture.Supported) return capture;
        float available = capture.Width - group.padding.left - group.padding.right;
        float minimum = children.Sum(c => c.Min) + group.spacing * 2, preferred = children.Sum(c => Math.Max(c.Min, c.Preferred)) + group.spacing * 2;
        if (!float.IsFinite(available) || available < minimum || children.Any(c => !float.IsFinite(c.Min) || !float.IsFinite(c.Preferred) || !float.IsFinite(c.Flexible) || c.Flexible < 0))
        { capture.Supported = false; capture.ProofFailure |= 32; return capture; }
        float interpolation = preferred == minimum ? 0 : Math.Clamp((available - minimum) / (preferred - minimum), 0, 1);
        float surplus = Math.Max(0, available - preferred), flexible = children.Sum(c => c.Flexible);
        float x = group.padding.left + (flexible == 0 ? surplus * ((int)group.childAlignment % 3) * .5f : 0);
        bool prediction = true;
        foreach (var c in children)
        {
            float width = c.Min + (Math.Max(c.Min, c.Preferred) - c.Min) * interpolation + (flexible > 0 ? surplus * c.Flexible / flexible : 0);
            float center = x + width * .5f; x += width + group.spacing;
            if (composite && c.Label) { center += (c.Preferred - width) * (.5f - c.Pivot); width = c.Preferred; }
            prediction &= Math.Abs(c.Width - width) <= .5f && Math.Abs(c.Center - center) <= .5f;
        }
        capture.Prediction = prediction;
        capture.Contained = children.All(c => c.MaskContained && Enumerable.Range(0, 4).All(i => Within(new Vector3(c.Corners[i * 2], c.Corners[i * 2 + 1], 0), rect.rect)));
        capture.NonOverlapping = children.Select((c, i) => i == 0 || children[i - 1].Center + children[i - 1].Width * .5f <= c.Center - c.Width * .5f + .05f).All(v => v);
        capture.Glyphs = GlyphContained(text);
        if (!capture.Prediction) capture.ProofFailure |= 64;
        if (!capture.Contained) capture.ProofFailure |= 128;
        if (!capture.NonOverlapping) capture.ProofFailure |= 256;
        return capture;
    }
    static bool ClusterBaseline(ClusterCapture c) => c.Supported && c.Prediction && c.Contained && c.NonOverlapping;
    static bool ClusterStable(ClusterCapture a, ClusterCapture b) => a.Inputs == b.Inputs && a.Children.Length == b.Children.Length &&
        a.Children.Zip(b.Children, (x, y) => x.Instance == y.Instance && Math.Abs(x.Width - y.Width) <= .05f && Math.Abs(x.Center - y.Center) <= .05f).All(v => v);
    static bool ClusterEqual(ClusterCapture a, ClusterCapture b) => ClusterBaseline(a) && ClusterBaseline(b) && b.Glyphs &&
        a.Driver == b.Driver && a.Inputs == b.Inputs && a.Outer == b.Outer && Math.Abs(a.Width - b.Width) <= .05f && Math.Abs(a.Height - b.Height) <= .05f &&
        a.Children.Length == b.Children.Length && a.Children.Zip(b.Children, (x, y) => x.Instance == y.Instance && x.Invariant == y.Invariant).All(v => v);
}
