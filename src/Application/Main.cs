using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Earthward.Domain;
using Earthward.Combat;
using Earthward.Rendering;
using Earthward.Presentation;
namespace Earthward.Application;

public partial class Main : Node2D
{
	public const string PerkProfilePath = "user://earthward_alien_chip_perks.json";
	// Persistence paths and logical world size. Sidebar layout never resizes the world.
	public const string SavePath = "user://earthward_checkpoint.json";
	public const string SettingsPath = "user://earthward_combat_settings.json";
	public static readonly Vector2 DesignSize = new(1440, 900);
	public Vector2 WorldSize { get; private set; } = DesignSize;

	// Composition root: managed simulation, campaign scheduling and native presentation.
	public DefenseState Game { get; private set; } = null!;
	public PlanetView Planet { get; private set; } = null!;
	public Battlefield Battle { get; private set; } = null!;
	public DefenseCampaignDirector Campaign { get; private set; } = null!;
	public ResearchGraphView ResearchGraph { get; private set; } = null!;
	public FactoryPerkSidebar FactoryPerkSidebar { get; private set; } = null!;
	public AircraftSpectator Spectator { get; private set; } = null!;
	public SoundBank Sounds { get; private set; } = null!;
	public Backdrop Background { get; private set; } = null!;
	public CombatHud BattleHud { get; private set; } = null!;

	// Visible command state; defeat pauses simulation but keeps the UI interactive.
	public bool DockOpen = true;
	public bool SolarNavOpen;
	public bool Started;
	public bool UserPaused;
	public bool Defeated;
	public bool CampaignWon;
	public bool SaveExists;
	public bool PreserveCheckpoint;
	public bool Dragging;
	public bool ResourceUpgradeMode;
	public string Tab = "build";
	public string Modal = "";
	public string SelectedBuild = "";
	public string Notice = "选择设施，再选择六边形地块建立第一道防线。";
	public double NoticeTime = 10;
	public double Speed = 1;
	public double NextWave = -1;
	public double Elapsed;
	public Vector2 Mouse;
	public string FocusId { get; private set; } = "earth";

	// Immediate-mode HUD hit regions, paint data and cached styles.
	public readonly List<DataMap> Buttons = new();
	public readonly List<Rect2> UiRects = new();
	private readonly List<DataMap> _hudItems = new();
	private readonly List<DataMap> _tooltips = new();
	private Vector2 _uiOffset;
	private readonly Dictionary<string, StyleBoxFlat> _boxStyles = new();
	private readonly Dictionary<string, Texture2D> _buildIcons = new();

	// Throttled UI refresh, save debounce and campaign checkpoint cadence.
	private double _uiRefresh;
	private double _graphRefreshDelay;
	private double _checkpointDebounce;
	private double _campaignRefresh;
	private double _campaignSaveElapsed;
	private bool _graphDirty = true;
	private bool _checkpointPending;
	private DataMap _campaignStatus = new();
	private DataMap _forecast = new();
	private double _forecastClock = -100;

	// Celestial navigation and deterministic capture command-line options.
	private List<DataMap> _celestialBodies = new();
	private string _capturePath = "";
	private double _captureAfter = 2.5;
	private bool _captureDone;
	private bool _demo;

	// Pointer ownership across building strokes, core upgrades and camera drags.
	private bool _spectatorDockOpen = true;
	private bool _middleDragging;
	private bool _buildPainting;
	private bool _paintSavePending;
	private bool _paintShortageNotified;
	private bool _upgradePointerDown;
	private double _dragDistance;
	private Vector2 _dragStart;
	private Vector2 _paintPrevious;
	private readonly HashSet<int> _paintedCells = new();
	private int _upgradeHoverSite = -1;

	// World target picking captures a moving aircraft on press and validates it on release.
	private DataMap _aircraftHover = new();
	private DataMap _aircraftPickCache = new();
	private DataMap _navigationPressedTarget = new();
	private Vector2 _aircraftPickPoint = new(-10000, -10000);
	private Vector2 _navigationPressOrigin;
	private long _aircraftPickTime = -1000;
	private double _navigationPressDistance;
	private bool _navigationCursorActive;

	// The research reveal mask animates independently of canvas scale and world camera.
	private const double ResearchRollDuration = .36;
	private const float RailWidth = 300;
	private bool _lastResearchLayout;
	private float _researchWidth = RailWidth;
	private float _researchTargetWidth = RailWidth;
	private Tween? _researchTween;
	private Control _researchClip = null!;

	// Native numeric controls stage a complete configuration before atomic application.
	private Control _combatControls = null!;
	public Dictionary<string, LineEdit> CombatFields { get; } = new();
	private string _combatPage = "base";
	private string _settingsNotice = "";
	private bool _settingsError;

	public override void _Ready()
	{
		if (TryRunNativeValidationScene()) return;
		GetWindow().Title = "EARTHWARD · 地球守望 1.27 | 深层研究 · CSV 数值表 · C#";
		CatalogData.Configure(file => Godot.FileAccess.GetFileAsString("res://data/domain/" + file));
		WorldSize = GetViewportRect().Size;
		Game = new DefenseState();
		CombatCatalog.Validate();
		if (!Game.FactoryPerks.LoadProfile(ProjectSettings.GlobalizePath(PerkProfilePath)))
			ShowNotice("永久特性档案读取失败，已保护原文件；请检查存档目录");
		LoadAchievements();
		Game.AlienChipDropped += OnAlienChipDropped;
		Game.Changed += InvalidateResearchGraph;
		foreach (string id in new[] { "mine", "solar", "interceptor", "laser", "missile", "shield", "satellite_launcher" })
		{
			string path = $"res://assets/ui/build_icons/{id}.png";
			if (ResourceLoader.Exists(path))
				_buildIcons[id] = GD.Load<Texture2D>(path);
		}
		LoadCombatPreferences();
		Background = new Backdrop { ZIndex = -20 };
		AddChild(Background);
		Planet = new PlanetView { Game = Game, ViewSize = new Vector2I((int)WorldSize.X, (int)WorldSize.Y), DefaultCameraDistance = WorldScale.DefaultCameraDistance, ZIndex = -10, ProcessPriority = -10 };
		AddChild(Planet);
		Planet.SlotSelected += SlotSelected;
		Planet.ResearchSatelliteLaunchCompleted += OnResearchSatelliteLaunchCompleted;
		Planet.CameraChanged += SyncBackgroundView;
		Planet.FocusChanged += OnFocusChanged;
		RefreshCelestialCatalog();
		SyncBackgroundView();
		Battle = new Battlefield(Game, Planet);
		Battle.WaveCompleted += WaveCompleted;
		Battle.WaveStarted += OnWaveStarted;
		Battle.EarthDestroyed += EarthDestroyed;
		Battle.InvasionDefeated += InvasionDefeated;
		Battle.EventNotice += ShowNotice;
		BattleHud = new CombatHud { App = this, ZIndex = -5 };
		AddChild(BattleHud);
	Battle.EarthDamaged += (amount, position) => { Planet.PulseAtmosphereHit(amount, position); NotifyEarthAttack(amount, position); NotifyFirstEarthAttackGuide(position); BattleHud.ShieldFlash = 1; };
		var intel = new CombatIntelHud { App = this };
		AddChild(intel);
		Spectator = new AircraftSpectator();
		AddChild(Spectator);
		Spectator.Setup(Planet, Battle);
		Spectator.ActiveChanged += OnSpectatorActiveChanged;
		Sounds = new SoundBank();
		AddChild(Sounds);
		Campaign = new DefenseCampaignDirector(Game, Battle);
		Campaign.EventNotice += ShowNotice;
		Campaign.CheckpointRequested += SaveIfSafe;
		_researchClip = new Control { Name = "ResearchScrollClip", ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore };
		AddChild(_researchClip);
		ResearchGraph = new ResearchGraphView();
		_researchClip.AddChild(ResearchGraph);
		ResearchGraph.PurchaseRequested += PurchaseGraph;
		ResearchGraph.FocusWindowRequested += FocusResearchWindow;
		ResearchGraph.Hide();
		FactoryPerkSidebar = new FactoryPerkSidebar();
		AddChild(FactoryPerkSidebar);
		FactoryPerkSidebar.Bind(Game);
		FactoryPerkSidebar.FeedbackRequested += (message, success) => { ShowNotice(message); Sounds.PlaySound(success ? "research" : "error"); if (success) SaveIfSafe(); };
		CreateCombatControls();
		GetViewport().SizeChanged += ResizeWorldView;
		ResizeWorldView();
		RefreshCampaignUi();
		InitializeTacticalAlerts();
		SaveExists = Godot.FileAccess.FileExists(SavePath);
		InitializeWaveRetry();
		PreserveCheckpoint = SaveExists;
		bool resume = false;
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--capture="))
				_capturePath = arg[10..];
			else if (arg.StartsWith("--capture-after=") && double.TryParse(arg[16..], NumberStyles.Float, CultureInfo.InvariantCulture, out var after))
				_captureAfter = after;
			else if (arg == "--continue")
				resume = true;
			else if (arg == "--demo")
				_demo = true;
			else if (arg == "--research-view")
				OpenResearchSidebar();
			else if (arg == "--settings-view")
				OpenCombatSettings();
		}
		if (resume && SaveExists)
			LoadCheckpoint();
		else if (_demo)
			StartWave();
		InitializeSatelliteGuide();
		SyncRender();
		QueueRedraw();
	}

	public override void _ExitTree()
	{
		FlushCheckpointWrites(false);
		if (Game != null && IsInstanceValid(GetViewport()))
			GetViewport().SizeChanged -= ResizeWorldView;
		if (Game != null)
		{
			Game.Changed -= InvalidateResearchGraph;
			Game.AlienChipDropped -= OnAlienChipDropped;
		}
		if (Planet != null && IsInstanceValid(Planet))
			Planet.ResearchSatelliteLaunchCompleted -= OnResearchSatelliteLaunchCompleted;
		if (IsInstanceValid(Planet))
		{
			Planet.CameraChanged -= SyncBackgroundView;
			Planet.SlotSelected -= SlotSelected;
			Planet.FocusChanged -= OnFocusChanged;
		}
	}

	private void ResizeWorldView()
	{
		WorldSize = GetViewportRect().Size;
		Planet.SetViewSize(new Vector2I((int)WorldSize.X, (int)WorldSize.Y));
		LayoutResearchSidebar(true);
		if (_combatControls != null)
			_combatControls.Position = (WorldSize - DesignSize) * .5f;
		LayoutFactoryPerks();
		SyncBackgroundView();
		QueueRedraw();
	}

	private void SyncBackgroundView()
	{
		if (!IsInstanceValid(Background) || !IsInstanceValid(Planet) || Planet.Camera == null)
			return;
		Background.SetView(Vector2.Zero, Planet.ViewSize, Planet.Camera.Fov);
		Background.SetCameraBasis(Planet.Camera.GlobalBasis);
	}

	private void RefreshCelestialCatalog() => _celestialBodies = Planet.GetCelestialBodies().ToList();

	public bool IsSpectating() => IsInstanceValid(Spectator) && Spectator.IsActive();

	public bool IsObserving() => FocusId != "earth" && !IsSpectating();

	public bool WorldIsPaused() => UserPaused || Modal != "" || Defeated;

	public override void _Process(double delta)
	{
		if (Game == null || Battle == null || Planet == null)
			return;
		long frameStart = CaptureFrameTimings ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
		PumpCheckpointWrites();
		Elapsed += delta;
		Vector2 previousMouse = Mouse;
		Mouse = GetGlobalMousePosition();
		bool paused = WorldIsPaused();
		AdvancePlayTime(delta);
		Battle.Paused = paused;
		Battle.SpeedScale = Speed;
		Planet.Paused = paused;
		Planet.PlacingBuilding = SelectedBuild != "" && !IsObserving();
		Planet.PlacingKind = SelectedBuild;
		_combatControls.Visible = Modal == "combat";
		Campaign.Paused = paused;
		Campaign.SpeedScale = Speed;
		_graphRefreshDelay = Math.Max(0, _graphRefreshDelay - delta);
		if (_checkpointPending)
		{
			_checkpointDebounce -= delta;
			if (_checkpointDebounce <= 0)
				QueueCheckpointSave();
		}
		LayoutResearchSidebar();
		UpdateLocalShieldGuide();
		LayoutFactoryPerks();
		ResearchGraph.Visible = ResearchSidebarPresent() && Modal == "";
		_researchClip.Visible = ResearchGraph.Visible;
		if (ResearchGraph.Visible && _graphDirty && _graphRefreshDelay <= 0)
			RefreshGraph();
		if ((Started || Campaign.OwnsEarthSchedule()) && !paused)
		{
			Game.Tick(delta * Speed);
			if (NextWave >= 0 && !Campaign.OwnsEarthSchedule())
			{
				NextWave -= delta * Speed;
				if (NextWave <= 0)
					StartWave();
			}
		}
		// The campaign keeps owning Earth waves after the first clear; CampaignWon is a HUD milestone.
		long combatStart = CaptureFrameTimings ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
		Battle.Step(delta);
		if (CaptureFrameTimings) LastCombatStepMs = FrameElapsed(combatStart);
		Campaign.Step(delta);
		UpdateLootPresentation(delta);
		UpdateTacticalAlerts(delta);
		UpdateFactoryCoverage(delta);
		UpdateShieldCoverage(delta);
		SyncRender();
		_campaignRefresh += delta;
		if (_campaignRefresh >= .2)
		{
			_campaignRefresh = 0;
			RefreshCampaignUi();
		}
		if (Campaign.OwnsEarthSchedule() && !paused && !PreserveCheckpoint)
		{
			_campaignSaveElapsed += delta;
			if (_campaignSaveElapsed >= 30)
				QueueCheckpointSave();
		}
		NoticeTime = Math.Max(0, NoticeTime - delta);
		UpdateAlienChipNotice();
		_uiRefresh += delta;
		if (_uiRefresh >= 1d / 60 || Mouse != previousMouse)
		{
			UpdateResourceUpgradeHover();
			UpdateNavigationHover();
			_uiRefresh = 0;
			QueueRedraw();
		}
		if (CaptureFrameTimings) LastMainProcessMs = FrameElapsed(frameStart);
		if (_capturePath != "" && Elapsed >= _captureAfter && !_captureDone)
		{
			_captureDone = true;
			Callable.From(Capture).CallDeferred();
		}
	}

	public void SyncRender()
	{
		if (Battle == null || !IsInstanceValid(Planet))
			return;
		long renderStart = CaptureFrameTimings ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
		_renderProjectiles.Clear();
		_renderProjectiles.AddRange(Battle.Shots);
		_renderProjectiles.AddRange(Battle.HostileShots);
		Planet.SyncCombatUnits(Battle.Drones, Battle.Enemies, _renderProjectiles, Battle.Beams, Battle.Bursts);
		Planet.SyncFactoryActivity(Battle.FactoryActivity);
		Planet.SyncLoot(Battle.LootPickups, Elapsed, _hoveredLootUid);
		Planet.SyncLocalShields(Battle.GetLocalShieldState());
		Planet.SetInvasionFronts(Battle.GetInvasionFronts());
		Planet.SyncUnitShields(Battle.Motherships.Values);
		BattleHud.QueueRedraw();
		if (CaptureFrameTimings) LastRenderSyncMs = FrameElapsed(renderStart);
	}

	private async void Capture()
	{
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		Error error = GetViewport().GetTexture().GetImage().SavePng(_capturePath);
		GD.Print("CAPTURE: ", _capturePath, " ", error);
		if (OS.GetCmdlineUserArgs().Contains("--quit-after-capture"))
			GetTree().Quit(error == Error.Ok ? 0 : 1);
	}

	private void InvalidateResearchGraph() => _graphDirty = true;

	public void ShowNotice(string message)
	{
		Notice = message;
		NoticeTime = 5.5;
		if (IsInstanceValid(ResearchGraph))
			ResearchGraph.Notice = message;
	}

	public void StartWave()
	{
		if (Campaign.OwnsEarthSchedule() || Battle.WaveRunning || Defeated || CampaignWon)
			return;
		if (!Started && !Game.FactoryPerks.ResetRunSites(Game.RunId))
		{
			ShowNotice("永久特性档案无法写入，本局尚未开始");
			return;
		}
		PreserveCheckpoint = false;
		Started = true;
		UserPaused = false;
		NextWave = -1;
		Battle.Active = true;
		Battle.StartWave();
		Sounds.PlaySound("start");
		ShowNotice($"第 {Game.Wave:00} 波来袭 · 工厂每 {FormatSetting(Game.CombatSettings.N("factory_spawn_interval"))} 秒补充一架，机群自动迎敌");
		UpdateFactoryPerkSites();
	}

	private void WaveCompleted()
	{
		if (Defeated)
			return;
		NextWave = -1;
		Callable.From(() => { QueueCheckpointSave(); }).CallDeferred();
		_graphDirty = true;
	}

	private void OnWaveStarted(long wave, DataMap plan)
	{
		CaptureWaveStartCheckpoint(wave);
		_forecast.Clear();
		NextWave = -1;
		_graphDirty = true;
		string hint = wave switch
		{
			1 => "击毁敌机后左键拾取掉落 · 飞抵左上角后资源入账",
			5 => "新方向出现 · 稀有资源核心与外星科技点需要拾取",
			10 => "外星光学情报就绪 · 可准备研究聚能激光",
			_ => ""
		};
		if (hint != "")
			ShowNotice($"第 {wave:00} 波 · {hint}");
	}

	private void InvasionDefeated()
	{
		ExitSpectator();
		// This milestone hands scheduling to Campaign; it must not open a blocking victory modal.
		CampaignWon = true;
		NextWave = -1;
		PreserveCheckpoint = false;
		Modal = "";
		Started = true;
		Campaign.SyncCampaign();
		SaveCheckpoint();
		_graphDirty = true;
		ShowNotice("近地母舰已清空 · 休整后敌军将拉远阵地，研究新的边界航程继续反击");
	}

	private void EarthDestroyed()
	{
		ClearFactoryCoverage();
		ExitSpectator();
		Defeated = true;
		Modal = "defeat";
		NextWave = -1;
		RecordDefeatAchievement();
	}

	private void RefreshCampaignUi()
	{
		_campaignStatus = Campaign.GetStatus();
		_campaignStatus["earth_under_attack"] = Battle.WaveRunning;
		_campaignStatus["science"] = Game.Science;
		UpdateFactoryPerkSites();
	}

	private void EnterSpectator()
	{
		if (Modal != "" || Defeated)
			return;
		if (!Spectator.Enter())
			ShowNotice("当前没有可以观战的在空战机");
		else
			ShowNotice("已进入战机观战 · V 切换，Esc 返回指挥视角");
	}

	private void NextSpectator()
	{
		if (Modal != "" || Defeated)
			return;
		if (!Spectator.NextAircraft())
			ShowNotice("当前没有可以观战的在空战机");
	}

	public void ExitSpectator()
	{
		if (IsSpectating())
			Spectator.Exit();
	}

	private void OnSpectatorActiveChanged(bool active)
	{
		if (active)
		{
			ClearFactoryCoverage();
			_spectatorDockOpen = DockOpen;
			CancelBuildSelection();
			CancelResourceUpgrade();
			SolarNavOpen = false;
			DockOpen = false;
		}
		else
		{
			DockOpen = _spectatorDockOpen;
			ShowNotice("已返回指挥视角");
		}
		LayoutResearchSidebar(true);
		SyncRender();
	}

	private void OnFocusChanged(string id)
	{
		FocusId = id;
		if (IsObserving())
		{
			ClearFactoryCoverage();
			CancelBuildSelection();
			CancelResourceUpgrade();
			SolarNavOpen = false;
			DockOpen = false;
		}
		ClearNavigationFeedback();
		QueueRedraw();
	}

	private void FocusBody(string id)
	{
		ClearFactoryCoverage();
		ExitSpectator();
		CancelBuildSelection();
		CancelResourceUpgrade();
		Planet.FocusBody(id);
		FocusId = id;
		SolarNavOpen = false;
	}

	private void FocusSystem()
	{
		ClearFactoryCoverage();
		ExitSpectator();
		CancelBuildSelection();
		CancelResourceUpgrade();
		Planet.FocusSystem();
		FocusId = "system";
		SolarNavOpen = false;
		DockOpen = false;
	}

	private void UpdateFactoryPerkSites()
	{
		if (FactoryPerkSidebar == null || Planet == null)
			return;
		if (Tab == "perks" && DockOpen)
			FactoryPerkSidebar.SetSites(Started ? Planet.GetFactorySites() : Array.Empty<DataMap>());
	}

	private void LayoutFactoryPerks()
	{
		if (FactoryPerkSidebar == null)
			return;
		FactoryPerkSidebar.Position = new Vector2(WorldSize.X - 306, 188);
		FactoryPerkSidebar.Size = new Vector2(276, Math.Max(300, Math.Min(480, WorldSize.Y - 270)));
		FactoryPerkSidebar.Visible = DockOpen && Tab == "perks" && Modal == "" && !ResearchSidebarPresent() && !IsSpectating();
	}

	private void OpenFactoryPerks()
	{
		ExitSpectator();
		CancelBuildSelection();
		CancelResourceUpgrade();
		SolarNavOpen = false;
		Tab = "perks";
		DockOpen = true;
		Modal = "";
		CloseCombatSettings(false);
		UpdateFactoryPerkSites();
		FactoryPerkSidebar.Refresh();
		LayoutResearchSidebar(true);
		LayoutFactoryPerks();
	}
}
