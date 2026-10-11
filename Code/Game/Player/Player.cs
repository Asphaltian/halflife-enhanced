using System;
using GoldSrc;
using GoldSrc.Bsp;

namespace HalfLife;

public enum PlayerAnim
{
	Idle,
	Walk,
	Jump,
	SuperJump,
	Die,
	Attack1
}

[LinkEntityToClass( "player" )]
public partial class Player : BaseMonster
{
	public const float FatalFallSpeed = 1024f;
	public const float MaxSafeFallSpeed = 580f;
	public const float DamageForFallSpeed = 100f / (FatalFallSpeed - MaxSafeFallSpeed);
	public const float MinBounceSpeed = 200f;
	public const float FallPunchThreshold = 350f;

	public const int PhysicsFlagOnLadder = 1 << 0;
	public const int PhysicsFlagOnSwing = 1 << 0;
	public const int PhysicsFlagOnTrain = 1 << 1;
	public const int PhysicsFlagOnBarnacle = 1 << 2;
	public const int PhysicsFlagDucking = 1 << 3;
	public const int PhysicsFlagUsing = 1 << 4;
	public const int PhysicsFlagObserver = 1 << 5;

	public const int WeaponSuit = 31;
	public const int WeaponAllWeapons = ~(1 << WeaponSuit);

	public const int MaxItems = 5;

	public const int HideHudWeapons = 1 << 0;
	public const int HideHudFlashlight = 1 << 1;
	public const int HideHudAll = 1 << 2;
	public const int HideHudHealth = 1 << 3;

	public const int TrainActive = 0x80;
	public const int TrainNew = 0xc0;
	public const int TrainOff = 0x00;
	public const int TrainNeutral = 0x01;
	public const int TrainSlow = 0x02;
	public const int TrainMedium = 0x03;
	public const int TrainFast = 0x04;
	public const int TrainBack = 0x05;

	private const int ObserverNone = 0;
	private const int ObserverChaseLocked = 1;
	private const int ObserverChaseFree = 2;
	private const int ObserverRoaming = 3;
	internal const int ObserverInEye = 4;
	private const int ObserverMapFree = 5;
	private const int ObserverMapChase = 6;

	private const float AirTime = 12f;
	private const float SearchRadius = 64f;

	public const string SoundFlashlightOn = "items/flashlight1.wav";
	public const string SoundFlashlightOff = "items/flashlight1.wav";

	public static readonly Vector3 HullMin = new( -16f, -16f, -36f );
	public static readonly Vector3 HullMax = new( 16f, 16f, 36f );
	public static readonly Vector3 DuckHullMin = new( -16f, -16f, -18f );
	private static readonly Vector3 DuckHullMax = new( 16f, 16f, 18f );
	private static readonly Vector3 View = new( 0f, 0f, 28f );

	public int RandomSeed;
	public int PlayerSound;

	[Save( FieldType.Integer )]
	public int TargetVolume;
	[Save( FieldType.Integer )]
	public int WeaponVolume;
	[Save( FieldType.Integer )]
	public int ExtraSoundTypes;
	[Save( FieldType.Integer )]
	public int WeaponFlash;
	public float StopExtraSoundTime;

	[Save( FieldType.Time )]
	private float _flashLightTime;
	[Save( FieldType.Integer )]
	private int _flashBattery;

	[Save( FieldType.Integer )]
	public int ButtonLast;
	[Save( FieldType.Integer )]
	public int ButtonPressed;
	[Save( FieldType.Integer )]
	public int ButtonReleased;

	public Edict SoundLast;
	public float SoundRoomType;
	public float SoundRange;

	[Save( FieldType.Float )]
	public float LastFallVelocity;

	[Save( FieldType.Integer )]
	public int[] Items = new int[MaxItems];
	private bool _knownItem;

	[Save( FieldType.Integer )]
	public int PhysicsFlags;
	public float NextSuicideTime;

	[Save( FieldType.Time )]
	private float _suitUpdate;
	[Save( FieldType.Integer )]
	private int[] _suitPlayList = new int[SuitPlayListSize];
	[Save( FieldType.Integer )]
	private int _suitPlayNext;
	[Save( FieldType.Integer )]
	private int[] _suitNoRepeat = new int[SuitNoRepeatSize];
	[Save( FieldType.Time )]
	private float[] _suitNoRepeatTime = new float[SuitNoRepeatSize];

	[Save( FieldType.Integer )]
	private int _lastDamageAmount;
	[Save( FieldType.Time )]
	private float _timeBasedDamagePrev;

	public float GeigerRange;
	private float _geigerDelay;
	private int _geigerRangePrev;

	[Save( FieldType.Integer )]
	private int _drownDamage;
	[Save( FieldType.Integer )]
	private int _drownRestored;
	[Save( FieldType.Integer )]
	private int _hudDamageBits;
	[Save( FieldType.Boolean )]
	private bool _initHud;
	private bool _gameHudInitialized;
	[Save( FieldType.Integer )]
	public int Train;
	public bool WeaponSent;

	[Save( FieldType.EHandle )]
	private BaseEntity _tank;
	public float DeadTime;

	private bool _noPlayerSound;
	[Save( FieldType.Boolean )]
	public bool LongJump;
	[Save( FieldType.Time )]
	private float _sneaking;

	private int _clientHealth;
	private int _clientBattery;
	[Save( FieldType.Integer )]
	public int HideHud;
	private int _clientHideHud;
	[Save( FieldType.Integer )]
	public int FieldOfViewSetting;
	public int ClientFieldOfView;

	[Save( FieldType.ClassPtr )]
	public BasePlayerItem[] PlayerItems = new BasePlayerItem[BasePlayerItem.MaxItemTypes];
	[Save( FieldType.ClassPtr )]
	public BasePlayerItem ActiveItem;
	public BasePlayerItem ClientActiveItem;
	[Save( FieldType.ClassPtr )]
	public BasePlayerItem LastItem;

	[Save( FieldType.Integer )]
	public int[] Ammo = new int[BasePlayerItem.MaxAmmoSlots];
	private readonly int[] _ammoLast = new int[BasePlayerItem.MaxAmmoSlots];

	public Vector3 AutoAim;
	public bool OnTarget;
	public int Deaths;
	private float _respawnTimer;
	private int _lastX;
	private int _lastY;
	private int _customSprayFrames;
	public float NextDecalTime;
	public string TeamName = "";
	public string AnimExtension = "";

	public float StartCharge;
	public float AmmoStartCharge;
	public float PlayAftershock;
	public float NextAmmoBurn;

	public const float ChatInterval = 1.0f;

	public float NextChatTime;
	public int AutoWeaponSwitch;

	public int Ammo9mm;
	public int Ammo357;
	public int AmmoBolts;
	public int AmmoBuckshot;
	public int AmmoRockets;
	public int AmmoUranium;
	public int AmmoHornets;
	public int AmmoArGrenades;

	public BaseEntity Tank
	{
		get => _tank.IsValid() ? _tank : null;
		set => _tank = value;
	}

	public bool IsObserver => IUser1 != 0;

	public override bool IsPlayer => true;

	public override bool IsNetClient => true;

	public override bool IsAlive => DeadFlag == DeadNo && Health > 0f;

	public override bool IsSneaking => _sneaking <= Server.Globals.Time;

	public override string TeamId => TeamName;

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public bool IsOnLadder => MoveType == MoveType.Fly;

	public override int Classify() => ClassPlayer;

	public override bool ShouldFadeOnDeath() => false;

	public override Vector3 GetGunPosition() => ClientSide ? Vector3.Zero : Origin + ViewOffset;

	public int ViewModelIndex;

	public override Vector3 BodyTarget( Vector3 source ) => Center + (ViewOffset * Server.RandomFloat( 0.5f, 1.1f ));

	public override void StartSneaking() => _sneaking = Server.Globals.Time - 1f;

	public override void StopSneaking() => _sneaking = Server.Globals.Time + 30f;

	public override void PlayerPreThink() => PreThink();

	public override void PlayerPostThink() => PostThink();

	public override void Spawn()
	{
		if ( ClientSide )
		{
			ActiveItem?.Deploy();

			ClientWeapons.RunningGaussPrediction = false;
			return;
		}

		StartCharge = Server.Globals.Time;

		ClassName = "player";
		Health = 100f;
		ArmorValue = 0f;
		DamageMode = DamageAim;
		Solid = Solid.SlideBox;
		MoveType = MoveType.Walk;
		MaxHealth = Health;
		EntityFlags &= EntityFlags.Proxy;
		EntityFlags |= EntityFlags.Client;
		AirFinished = Server.Globals.Time + 12f;
		Damage = 2f;
		Effects = 0;
		DeadFlag = DeadNo;
		DamageTake = 0f;
		DamageSave = 0f;
		Friction = 1f;
		Gravity = 1f;
		_hudDamageBits = -1;
		DamageTypeBits = 0;
		PhysicsFlags = 0;
		LongJump = false;

		Server.SetPhysicsKeyValue( this, "slj", "0" );
		Server.SetPhysicsKeyValue( this, "hl", "1" );

		Fov = FieldOfViewSetting = 0;
		ClientFieldOfView = -1;

		NextDecalTime = 0f;
		_geigerDelay = (float)(Server.Globals.Time + 2.0);

		FieldOfView = 0.5f;

		MonsterBloodColor = BloodColorRed;
		NextAttack = BasePlayerWeapon.WeaponTimeBase;
		StartSneaking();

		_flashBattery = 99;
		_flashLightTime = 1f;

		LastFallVelocity = 0f;

		HalfLifeDll.GameRules.SetDefaultPlayerTeam( this );
		HalfLifeDll.GameRules.GetPlayerSpawnSpot( this );

		SetModel( "models/player.mdl" );
		HalfLifeDll.ModelIndexPlayer = ModelIndex;
		Sequence = LookupActivity( (int)Activity.Idle );

		if ( (EntityFlags & EntityFlags.Ducking) == EntityFlags.Ducking )
			SetSize( DuckHullMin, DuckHullMax );
		else
			SetSize( HullMin, HullMax );

		ViewOffset = View;
		Precache();
		HackedGunPos = new Vector3( 0f, 32f, 0f );

		if ( PlayerSound == SoundEnt.ListEmpty )
			Util.Alert( AlertType.Console, "Couldn't alloc player sound slot!\n" );

		_noPlayerSound = false;

		LastItem = null;
		_initHud = true;
		_clientHideHud = -1;
		WeaponSent = false;
		ClientActiveItem = null;
		_clientBattery = -1;

		for ( var i = 0; i < BasePlayerItem.MaxAmmoSlots; i++ )
		{
			Ammo[i] = 0;
			_ammoLast[i] = 0;
		}

		_lastX = _lastY = 0;

		NextChatTime = Server.Globals.Time;

		HalfLifeDll.GameRules.PlayerSpawn( this );
	}

	public override void Precache()
	{
		if ( ClientSide )
			return;

		var graph = HalfLifeDll.WorldGraph;

		if ( graph.GraphPresent && !graph.GraphPointersSet )
		{
			if ( !graph.SetGraphPointers() )
				Util.Alert( AlertType.Console, "**Graph pointers were not set!\n" );
			else
				Util.Alert( AlertType.Console, "**Graph Pointers Set!\n" );
		}

		GeigerRange = 1000f;
		_geigerRangePrev = 1000;

		DamageTypeBits = 0;
		_hudDamageBits = -1;

		_clientBattery = -1;

		Train = TrainNew;

		UserMessages.Link();

		if ( HalfLifeDll.InitHud )
			_initHud = true;
	}

	public override bool Restore( Restore restore )
	{
		if ( !base.Restore( restore ) )
			return false;

		var saveData = Server.Globals.SaveData;

		if ( !saveData.UseLandmark )
		{
			Util.Alert( AlertType.Console, $"No Landmark:{saveData.LandmarkName}\n" );

			var spot = EntSelectSpawnPoint();

			Origin = spot.Origin + new Vector3( 0f, 0f, 1f );
			Angles = spot.Angles;
		}

		ViewAngle = new Vector3( ViewAngle.x, ViewAngle.y, 0f );
		Angles = ViewAngle;

		FixAngle = 1;

		MonsterBloodColor = BloodColorRed;

		HalfLifeDll.ModelIndexPlayer = ModelIndex;

		if ( (EntityFlags & EntityFlags.Ducking) == EntityFlags.Ducking )
			SetSize( DuckHullMin, DuckHullMax );
		else
			SetSize( HullMin, HullMax );

		Server.SetPhysicsKeyValue( this, "hl", "1" );
		Server.SetPhysicsKeyValue( this, "slj", LongJump ? "1" : "0" );

		TabulateAmmo();

		NextAttack = BasePlayerWeapon.WeaponTimeBase;

		return true;
	}

	public virtual void PreThink()
	{
		var buttonsChanged = ButtonLast ^ Button;

		ButtonPressed = buttonsChanged & Button;
		ButtonReleased = buttonsChanged & ~Button;

		HalfLifeDll.GameRules.PlayerThink( this );

		if ( HalfLifeDll.GameOver )
			return;

		Server.Globals.MakeVectors( ViewAngle );

		ItemPreFrame();
		WaterMove();

		if ( HalfLifeDll.GameRules is not null && HalfLifeDll.GameRules.AllowFlashlight() )
			HideHud &= ~HideHudFlashlight;
		else
			HideHud |= HideHudFlashlight;

		UpdateClientData();

		CheckTimeBasedDamage();

		CheckSuitUpdate();

		if ( IsObserver )
		{
			ObserverHandleButtons();
			ObserverCheckTarget();
			ObserverCheckProperties();
			Impulse = 0;
			return;
		}

		if ( DeadFlag >= DeadDying )
		{
			PlayerDeathThink();
			return;
		}

		if ( (PhysicsFlags & PhysicsFlagOnTrain) != 0 )
			EntityFlags |= EntityFlags.OnTrain;
		else
			EntityFlags &= ~EntityFlags.OnTrain;

		if ( (PhysicsFlags & PhysicsFlagOnTrain) != 0 )
		{
			var train = (GroundEntity ?? Server.GetEdict( 0 )) as BaseEntity;

			if ( train is null )
			{
				var trainTrace = Util.TraceLine( Origin, Origin + new Vector3( 0f, 0f, -38f ), IgnoreMonsters, this );

				if ( trainTrace.Fraction != 1f && trainTrace.Ent is not null )
					train = trainTrace.Ent as BaseEntity;

				if ( train is null || (train.ObjectCaps & ObjectCaps.DirectionalUse) != ObjectCaps.DirectionalUse || !train.OnControls( this ) )
				{
					PhysicsFlags &= ~PhysicsFlagOnTrain;
					Train = TrainNew | TrainOff;

					if ( train is not null && train.Classify() == ClassVehicle )
						((FuncVehicle)train).Driver = null;

					return;
				}
			}
			else if ( (EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround
				|| (train.SpawnFlags & FuncTrackTrain.SpawnFlagNoControl) != 0
				|| ((Button & (int)(InButtons.MoveLeft | InButtons.MoveRight)) != 0 && train.Classify() != ClassVehicle) )
			{
				PhysicsFlags &= ~PhysicsFlagOnTrain;
				Train = TrainNew | TrainOff;

				if ( train.Classify() == ClassVehicle )
					((FuncVehicle)train).Driver = null;

				return;
			}

			Velocity = Vector3.Zero;

			var velocity = 0f;

			if ( train.Classify() == ClassVehicle )
			{
				if ( (Button & (int)InButtons.Forward) != 0 )
				{
					velocity = 1f;
					train.Use( this, this, UseType.Set, velocity );
				}

				if ( (Button & (int)InButtons.Back) != 0 )
				{
					velocity = -1f;
					train.Use( this, this, UseType.Set, velocity );
				}

				if ( (Button & (int)InButtons.MoveLeft) != 0 )
				{
					velocity = 20f;
					train.Use( this, this, UseType.Set, velocity );
				}

				if ( (Button & (int)InButtons.MoveRight) != 0 )
				{
					velocity = 30f;
					train.Use( this, this, UseType.Set, velocity );
				}
			}
			else if ( (ButtonPressed & (int)InButtons.Forward) != 0 )
			{
				velocity = 1f;
				train.Use( this, this, UseType.Set, velocity );
			}
			else if ( (ButtonPressed & (int)InButtons.Back) != 0 )
			{
				velocity = -1f;
				train.Use( this, this, UseType.Set, velocity );
			}

			if ( velocity != 0f )
			{
				Train = TrainSpeed( (int)train.Speed, train.Impulse );
				Train |= TrainActive | TrainNew;
			}
		}
		else if ( (Train & TrainActive) != 0 )
		{
			Train = TrainNew;
		}

		if ( (Button & (int)InButtons.Jump) != 0 )
			Jump();

		if ( (Button & (int)InButtons.Duck) != 0 || (EntityFlags & EntityFlags.Ducking) == EntityFlags.Ducking || (PhysicsFlags & PhysicsFlagDucking) != 0 )
			Duck();

		if ( (EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround )
			LastFallVelocity = -Velocity.z;

		EnemyEntity = null;

		if ( (PhysicsFlags & PhysicsFlagOnBarnacle) != 0 )
			Velocity = Vector3.Zero;
	}

	public virtual void PostThink()
	{
		if ( !HalfLifeDll.GameOver && IsAlive )
		{
			if ( Tank is { } tank )
			{
				if ( tank.OnControls( this ) && string.IsNullOrEmpty( WeaponModel ) )
				{
					tank.Use( this, this, UseType.Set, 2f );
				}
				else
				{
					tank.Use( this, this, UseType.Off, 0f );
					Tank = null;
				}
			}

			ItemPostFrame();

			if ( (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround && Health > 0f && LastFallVelocity >= FallPunchThreshold )
			{
				if ( WaterType != (int)Contents.Water && LastFallVelocity > MaxSafeFallSpeed )
				{
					var fallDamage = HalfLifeDll.GameRules.PlayerFallDamage( this );

					if ( fallDamage > Health )
						Sound.Emit( this, SoundChannel.Item, "common/bodysplat.wav", 1f, Server.AttenuationNormal );

					if ( fallDamage > 0f )
					{
						TakeDamage( World, World, fallDamage, DamageFall );
						PunchAngle = new Vector3( 0f, PunchAngle.y, PunchAngle.z );
					}
				}

				if ( IsAlive )
					SetAnimation( PlayerAnim.Walk );
			}

			if ( (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
			{
				if ( LastFallVelocity > 64f && !HalfLifeDll.GameRules.IsMultiplayer() )
					SoundEnt.InsertSound( WorldSound.Player, Origin, (int)LastFallVelocity, 0.2f );

				LastFallVelocity = 0f;
			}

			if ( IsAlive )
			{
				if ( Velocity.x == 0f && Velocity.y == 0f )
					SetAnimation( PlayerAnim.Idle );
				else if ( (Velocity.x != 0f || Velocity.y != 0f) && (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
					SetAnimation( PlayerAnim.Walk );
				else if ( WaterLevel > 1 )
					SetAnimation( PlayerAnim.Walk );
			}

			StudioFrameAdvance();

			if ( Health > 0f )
				ModelIndex = HalfLifeDll.ModelIndexPlayer;

			UpdatePlayerSound();
		}

		var frameTime = Server.Globals.FrameTime;

		for ( var i = 0; i < BasePlayerItem.MaxItemTypes; i++ )
		{
			for ( var item = PlayerItems[i]; item is not null; item = item.Next )
			{
				if ( item.GetWeaponPtr() is not { UseDecrement: true } gun )
					continue;

				gun.NextPrimaryAttack = Math.Max( gun.NextPrimaryAttack - frameTime, -1f );
				gun.NextSecondaryAttack = Math.Max( gun.NextSecondaryAttack - frameTime, -0.001f );

				if ( gun.TimeWeaponIdle != 1000f )
					gun.TimeWeaponIdle = Math.Max( gun.TimeWeaponIdle - frameTime, -0.001f );

				if ( gun.FUser1 != 1000f )
					gun.FUser1 = Math.Max( gun.FUser1 - frameTime, -0.001f );
			}
		}

		NextAttack -= frameTime;

		if ( NextAttack < -0.001 )
			NextAttack = -0.001f;

		if ( NextAmmoBurn != 1000f )
		{
			NextAmmoBurn -= frameTime;

			if ( NextAmmoBurn < -0.001 )
				NextAmmoBurn = -0.001f;
		}

		if ( AmmoStartCharge != 1000f )
		{
			AmmoStartCharge -= frameTime;

			if ( AmmoStartCharge < -0.001 )
				AmmoStartCharge = -0.001f;
		}

		ButtonLast = Button;
	}

	public virtual void Jump()
	{
		if ( (EntityFlags & EntityFlags.WaterJump) == EntityFlags.WaterJump )
			return;

		if ( WaterLevel >= 2 )
			return;

		if ( (ButtonPressed & (int)InButtons.Jump) == 0 )
			return;

		if ( (EntityFlags & EntityFlags.OnGround) != EntityFlags.OnGround || GroundEntity is null )
			return;

		Server.Globals.MakeVectors( Angles );

		SetAnimation( PlayerAnim.Jump );

		if ( LongJump && (Button & (int)InButtons.Duck) != 0 && DuckTime > 0 && MathLib.Length( Velocity ) > 50f )
			SetAnimation( PlayerAnim.SuperJump );

		var ground = GroundEntity;

		if ( (ground.EntityFlags & EntityFlags.Conveyor) == EntityFlags.Conveyor )
			Velocity += BaseVelocity;

		if ( ground.ClassName == "func_vehicle" )
			Velocity += ground.Velocity;
	}

	public virtual void Duck()
	{
		if ( (Button & (int)InButtons.Duck) != 0 && IdealActivity != Activity.Leap )
			SetAnimation( PlayerAnim.Walk );
	}

	public void WaterMove()
	{
		if ( MoveType == MoveType.NoClip )
			return;

		if ( Health < 0f )
			return;

		if ( WaterLevel != 3 )
		{
			if ( AirFinished < Server.Globals.Time )
				Sound.Emit( this, SoundChannel.Voice, "player/pl_wade1.wav", 1f, Server.AttenuationNormal );
			else if ( AirFinished < Server.Globals.Time + 9f )
				Sound.Emit( this, SoundChannel.Voice, "player/pl_wade2.wav", 1f, Server.AttenuationNormal );

			AirFinished = Server.Globals.Time + AirTime;
			Damage = 2f;

			if ( _drownDamage > _drownRestored )
			{
				DamageTypeBits |= DamageDrownRecover;
				DamageTypeBits &= ~DamageDrown;
				TimeBasedDamage[TimeBasedDrownRecover] = 0;
			}
		}
		else
		{
			DamageTypeBits &= ~DamageDrownRecover;
			TimeBasedDamage[TimeBasedDrownRecover] = 0;

			if ( AirFinished < Server.Globals.Time )
			{
				if ( PainFinished < Server.Globals.Time )
				{
					Damage += 1f;

					if ( Damage > 5f )
						Damage = 5f;

					TakeDamage( World, World, Damage, DamageDrown );
					PainFinished = Server.Globals.Time + 1f;

					_drownDamage += (int)Damage;
				}
			}
			else
			{
				DamageTypeBits &= ~DamageDrown;
			}
		}

		if ( WaterLevel == 0 )
		{
			EntityFlags &= ~EntityFlags.InWater;
			return;
		}

		var air = (int)(AirFinished - Server.Globals.Time);

		if ( Server.RandomLong( 0, 0x1f ) == 0 && Server.RandomLong( 0, (int)AirTime - 1 ) >= air )
		{
			switch ( Server.RandomLong( 0, 3 ) )
			{
				case 0:
					Sound.Emit( this, SoundChannel.Body, "player/pl_swim1.wav", 0.8f, Server.AttenuationNormal );
					break;
				case 1:
					Sound.Emit( this, SoundChannel.Body, "player/pl_swim2.wav", 0.8f, Server.AttenuationNormal );
					break;
				case 2:
					Sound.Emit( this, SoundChannel.Body, "player/pl_swim3.wav", 0.8f, Server.AttenuationNormal );
					break;
				case 3:
					Sound.Emit( this, SoundChannel.Body, "player/pl_swim4.wav", 0.8f, Server.AttenuationNormal );
					break;
			}
		}

		if ( WaterType == (int)Contents.Lava )
		{
			if ( DamageTime < Server.Globals.Time )
				TakeDamage( World, World, 10 * WaterLevel, DamageBurn );
		}
		else if ( WaterType == (int)Contents.Slime )
		{
			DamageTime = Server.Globals.Time + 1f;
			TakeDamage( World, World, 4 * WaterLevel, DamageAcid );
		}

		if ( (EntityFlags & EntityFlags.InWater) != EntityFlags.InWater )
		{
			EntityFlags |= EntityFlags.InWater;
			DamageTime = 0f;
		}
	}

	private void PlayerDeathThink()
	{
		if ( (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
		{
			var forward = MathLib.Length( Velocity ) - 20f;

			Velocity = forward <= 0f ? Vector3.Zero : forward * Util.Normalize( Velocity );
		}

		if ( HasWeapons() )
			PackDeadPlayerItems();

		if ( ModelIndex != 0 && !SequenceFinished && DeadFlag == DeadDying )
		{
			StudioFrameAdvance();

			_respawnTimer += Server.Globals.FrameTime;

			if ( _respawnTimer < 4f )
				return;
		}

		if ( DeadFlag == DeadDying )
		{
			if ( SequenceFinished && HalfLifeDll.GameRules.IsMultiplayer() && MoveType == MoveType.None )
			{
				HalfLifeDll.CopyToBodyQue( this );
				ModelIndex = 0;
			}

			DeadFlag = DeadDead;
		}

		if ( MoveType != MoveType.None && (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround )
			MoveType = MoveType.None;

		StopAnimation();

		Effects |= Effects.NoInterp;
		FrameRate = 0f;

		var anyButtonDown = (Button & ~(int)InButtons.Score) != 0;

		if ( DeadFlag == DeadDead )
		{
			if ( anyButtonDown )
				return;

			if ( HalfLifeDll.GameRules.PlayerCanRespawn( this ) )
			{
				DeadTime = Server.Globals.Time;
				DeadFlag = DeadRespawnable;
			}

			return;
		}

		if ( HalfLifeDll.GameRules.IsMultiplayer() && Server.Globals.Time > DeadTime + 6f && (PhysicsFlags & PhysicsFlagObserver) == 0 )
			StartDeathCam();

		if ( IUser1 != 0 )
			return;

		if ( !anyButtonDown && !(HalfLifeDll.GameRules.IsMultiplayer() && Cvar.GetFloat( "mp_forcerespawn" ) > 0f && Server.Globals.Time > DeadTime + 5f) )
			return;

		Button = 0;
		_respawnTimer = 0f;

		HalfLifeDll.Respawn( this, (PhysicsFlags & PhysicsFlagObserver) == 0 );
		NextThink = -1f;
	}

	public void StartDeathCam()
	{
		if ( MathLib.VectorCompare( ViewOffset, Vector3.Zero ) )
			return;

		var spot = Server.FindEntityByString( null, "classname", "info_intermission" );

		if ( !Util.IsNullEnt( spot ) )
		{
			for ( var random = Server.RandomLong( 0, 3 ); random > 0; random-- )
			{
				if ( Server.FindEntityByString( spot, "classname", "info_intermission" ) is { } newSpot )
					spot = newSpot;
			}

			HalfLifeDll.CopyToBodyQue( this );

			SetOrigin( spot.Origin );
			Angles = ViewAngle = spot.ViewAngle;
		}
		else
		{
			HalfLifeDll.CopyToBodyQue( this );

			var trace = Util.TraceLine( Origin, Origin + new Vector3( 0f, 0f, 128f ), IgnoreMonsters, this );

			SetOrigin( trace.EndPos );
			Angles = ViewAngle = MathLib.VectorAngles( trace.EndPos - Origin );
		}

		PhysicsFlags |= PhysicsFlagObserver;
		ViewOffset = Vector3.Zero;
		FixAngle = 1;
		Solid = Solid.Not;
		DamageMode = DamageNo;
		MoveType = MoveType.None;
		ModelIndex = 0;
	}

	public void StartObserver( Vector3 position, Vector3 viewAngle )
	{
		Server.MessageBegin( MessageDest.Pas, Server.TempEntityMessage, Origin );
		Server.WriteByte( TempEntity.KillPlayerAttachments );
		Server.WriteByte( (byte)Index );
		Server.MessageEnd();

		ActiveItem?.Holster();

		if ( Tank is { } tank )
		{
			tank.Use( this, this, UseType.Off, 0f );
			Tank = null;
		}

		SetSuitUpdate( null, false, 0 );

		Server.MessageBegin( MessageDest.One, UserMessages.CurWeapon, default, this );
		Server.WriteByte( 0 );
		Server.WriteByte( 0xFF );
		Server.WriteByte( 0xFF );
		Server.MessageEnd();

		FieldOfViewSetting = ClientFieldOfView = 0;
		Fov = FieldOfViewSetting;

		Server.MessageBegin( MessageDest.One, UserMessages.SetFov, default, this );
		Server.WriteByte( 0 );
		Server.MessageEnd();

		HideHud = HideHudHealth | HideHudWeapons;
		PhysicsFlags |= PhysicsFlagObserver;
		Effects = Effects.NoDraw;
		ViewOffset = Vector3.Zero;
		Angles = ViewAngle = viewAngle;
		FixAngle = 1;
		Solid = Solid.Not;
		DamageMode = DamageNo;
		MoveType = MoveType.None;
		PhysicsFlags &= ~PhysicsFlagDucking;
		EntityFlags &= ~EntityFlags.Ducking;
		DeadFlag = DeadRespawnable;
		Health = 1f;

		_initHud = true;

		Team = 0;

		Server.MessageBegin( MessageDest.All, UserMessages.TeamInfo );
		Server.WriteByte( Index );
		Server.WriteString( "" );
		Server.MessageEnd();

		RemoveAllItems( false );

		SetOrigin( position );

		_nextObserverInput = 0f;
		ObserverSetMode( _observerLastMode );
	}

	public void PlayerUse()
	{
		const int use = (int)InButtons.Use;

		if ( IsObserver )
			return;

		if ( ((Button | ButtonPressed | ButtonReleased) & use) == 0 )
			return;

		if ( (ButtonPressed & use) != 0 )
		{
			if ( Tank is { } tank )
			{
				tank.Use( this, this, UseType.Off, 0f );
				Tank = null;
				return;
			}

			var train = (GroundEntity ?? Server.GetEdict( 0 )) as BaseEntity;

			if ( (PhysicsFlags & PhysicsFlagOnTrain) != 0 )
			{
				PhysicsFlags &= ~PhysicsFlagOnTrain;
				Train = TrainNew | TrainOff;

				if ( train is not null && train.Classify() == ClassVehicle )
					((FuncVehicle)train).Driver = null;

				return;
			}

			if ( train is not null
				&& (Button & (int)InButtons.Jump) == 0
				&& (EntityFlags & EntityFlags.OnGround) == EntityFlags.OnGround
				&& (train.ObjectCaps & ObjectCaps.DirectionalUse) == ObjectCaps.DirectionalUse
				&& train.OnControls( this ) )
			{
				PhysicsFlags |= PhysicsFlagOnTrain;
				Train = TrainSpeed( (int)train.Speed, train.Impulse );
				Train |= TrainNew;

				if ( train.Classify() == ClassVehicle )
				{
					Sound.Emit( this, SoundChannel.Item, "plats/vehicle_ignition.wav", 0.8f, Server.AttenuationNormal );

					((FuncVehicle)train).Driver = this;
				}
				else
					Sound.Emit( this, SoundChannel.Item, "plats/train_use1.wav", 0.8f, Server.AttenuationNormal );

				return;
			}
		}

		Server.Globals.MakeVectors( ViewAngle );

		BaseEntity closest = null;
		BaseEntity found = null;
		var maxDot = ViewFieldNarrow;

		while ( (found = Util.FindEntityInSphere( found, Origin, SearchRadius )) is not null )
		{
			if ( (found.ObjectCaps & (ObjectCaps.ImpulseUse | ObjectCaps.ContinuousUse | ObjectCaps.OnOffUse)) == 0 )
				continue;

			var lineOfSight = Util.ClampVectorToBox( found.BModelOrigin - (Origin + ViewOffset), found.Size * 0.5f );
			var dot = Vector3.Dot( lineOfSight, Server.Globals.Forward );

			if ( dot > maxDot )
			{
				closest = found;
				maxDot = dot;
			}
		}

		if ( closest is null )
		{
			if ( (ButtonPressed & use) != 0 )
				Sound.Emit( this, SoundChannel.Item, "common/wpn_denyselect.wav", 0.4f, Server.AttenuationNormal );

			return;
		}

		var caps = closest.ObjectCaps;

		if ( (ButtonPressed & use) != 0 )
			Sound.Emit( this, SoundChannel.Item, "common/wpn_select.wav", 0.4f, Server.AttenuationNormal );

		if ( ((Button & use) != 0 && (caps & ObjectCaps.ContinuousUse) == ObjectCaps.ContinuousUse)
			|| ((ButtonPressed & use) != 0 && (caps & (ObjectCaps.ImpulseUse | ObjectCaps.OnOffUse)) != 0) )
		{
			if ( (caps & ObjectCaps.ContinuousUse) == ObjectCaps.ContinuousUse )
				PhysicsFlags |= PhysicsFlagUsing;

			closest.Use( this, this, UseType.Set, 1f );
		}
		else if ( (ButtonReleased & use) != 0 && (closest.ObjectCaps & ObjectCaps.OnOffUse) == ObjectCaps.OnOffUse )
		{
			closest.Use( this, this, UseType.Set, 0f );
		}
	}

	public override void AddPoints( int score, bool allowNegativeScore )
	{
		if ( score < 0 && !allowNegativeScore )
		{
			if ( Frags < 0f )
				return;

			if ( -score > Frags )
				score = (int)-Frags;
		}

		Frags += score;

		Server.MessageBegin( MessageDest.All, UserMessages.ScoreInfo );
		Server.WriteByte( Index );
		Server.WriteShort( (int)Frags );
		Server.WriteShort( Deaths );
		Server.WriteShort( 0 );
		Server.WriteShort( HalfLifeDll.GameRules.GetTeamIndex( TeamName ) + 1 );
		Server.MessageEnd();
	}

	public override void AddPointsToTeam( int score, bool allowNegativeScore )
	{
		for ( var i = 1; i <= Server.MaxClients; i++ )
		{
			var player = Util.PlayerByIndex( i );

			if ( player is not null && i != Index && HalfLifeDll.GameRules.PlayerRelationship( this, player ) == HalfLife.PlayerRelationship.Teammate )
				player.AddPoints( score, allowNegativeScore );
		}
	}

	public override bool BecomeProne()
	{
		PhysicsFlags |= PhysicsFlagOnBarnacle;
		return true;
	}

	public override void BarnacleVictimBitten( BaseEntity barnacle )
	{
		TakeDamage( barnacle, barnacle, Health + ArmorValue, DamageSlash | DamageAlwaysGib );
	}

	public override void BarnacleVictimReleased()
	{
		PhysicsFlags &= ~PhysicsFlagOnBarnacle;
	}

	public void EnableControl( bool control )
	{
		if ( !control )
			EntityFlags |= EntityFlags.Frozen;
		else
			EntityFlags &= ~EntityFlags.Frozen;
	}

	public void SetCustomDecalFrames( int frames )
	{
		_customSprayFrames = frames > 0 && frames < 8 ? frames : -1;
	}

	public int GetCustomDecalFrames() => _customSprayFrames;

	public void SetPrefsFromUserinfo( string autoWeaponSwitch )
	{
		AutoWeaponSwitch = string.IsNullOrEmpty( autoWeaponSwitch ) ? 1 : Util.Atoi( autoWeaponSwitch );
	}

	public Edict EntSelectSpawnPoint()
	{
		var rules = HalfLifeDll.GameRules;
		BaseEntity spot = null;

		if ( rules.IsCoOp() )
		{
			spot = Util.FindEntityByClassname( HalfLifeDll.LastSpawn, "info_player_coop" )
				?? Util.FindEntityByClassname( HalfLifeDll.LastSpawn, "info_player_start" );
		}
		else if ( rules.IsDeathmatch() )
		{
			spot = SelectDeathmatchSpawnPoint();
		}

		if ( spot is null )
		{
			var startSpot = Server.Globals.StartSpot;

			spot = string.IsNullOrEmpty( startSpot )
				? Util.FindEntityByClassname( null, "info_player_start" )
				: Util.FindEntityByTargetname( null, startSpot );
		}

		if ( spot is null )
		{
			Util.Alert( AlertType.Error, "PutClientInServer: no info_player_start on level" );
			return Server.GetEdict( 0 );
		}

		HalfLifeDll.LastSpawn = spot;

		return spot;
	}

	private BaseEntity SelectDeathmatchSpawnPoint()
	{
		var tries = 10;

		if ( HalfLifeDll.LastSpawn is null )
		{
			tries = 0;

			for ( var entity = Util.FindEntityByClassname( null, "info_player_deathmatch" ); entity is not null; entity = Util.FindEntityByClassname( entity, "info_player_deathmatch" ) )
				tries++;
		}

		var spot = HalfLifeDll.LastSpawn;

		for ( var i = Server.RandomLong( 1, tries - 1 ); i > 0; i-- )
			spot = Util.FindEntityByClassname( spot, "info_player_deathmatch" );

		spot ??= Util.FindEntityByClassname( spot, "info_player_deathmatch" );

		var firstSpot = spot;

		do
		{
			if ( spot is not null && IsSpawnPointValid( spot ) )
			{
				if ( MathLib.VectorCompare( spot.Origin, Vector3.Zero ) )
				{
					spot = Util.FindEntityByClassname( spot, "info_player_deathmatch" );
					continue;
				}

				return spot;
			}

			spot = Util.FindEntityByClassname( spot, "info_player_deathmatch" );
		}
		while ( spot != firstSpot );

		if ( spot is null )
			return null;

		BaseEntity blocker = null;

		while ( (blocker = Util.FindEntityInSphere( blocker, spot.Origin, 128f )) is not null )
		{
			if ( blocker.IsPlayer && blocker != this )
				blocker.TakeDamage( World, World, 300f, DamageGeneric );
		}

		return spot;
	}

	private bool IsSpawnPointValid( BaseEntity spot )
	{
		if ( !spot.IsTriggered( this ) )
			return false;

		BaseEntity entity = null;

		while ( (entity = Util.FindEntityInSphere( entity, spot.Origin, 128f )) is not null )
		{
			if ( entity.IsPlayer && entity != this )
				return false;
		}

		return true;
	}

	private static int TrainSpeed( int speed, int max )
	{
		var fraction = speed / (float)max;

		if ( speed < 0 )
			return TrainBack;

		if ( speed == 0 )
			return TrainNeutral;

		if ( fraction < 0.33 )
			return TrainSlow;

		if ( fraction < 0.66 )
			return TrainMedium;

		return TrainFast;
	}
}
