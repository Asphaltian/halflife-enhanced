using System;
using GoldSrc;

namespace HalfLife;

partial class HalfLifeClientDll
{
	private const int DeadDiscardBody = 4;

	private readonly BasePlayerWeapon[] _weapons = new BasePlayerWeapon[BasePlayerItem.MaxWeapons];

	private GameObject _weaponObjects;
	private Player _player;
	private Glock _glock;
	private Crowbar _crowbar;
	private Python _python;
	private Mp5 _mp5;
	private Crossbow _crossbow;
	private Shotgun _shotgun;
	private Rpg _rpg;
	private Gauss _gauss;
	private Egon _egon;
	private HornetGun _hornetGun;
	private HandGrenade _handGrenade;
	private Satchel _satchel;
	private Tripmine _tripmine;
	private Squeak _snark;

	private LocalState _finalState;
	private int _currentAnim;
	private int _lastHealth;
	private Vector3 _previousOrigin;

	public GlobalVars Globals { get; } = new();

	public float ApplyVelocity { get; set; }

	public bool RunningGaussPrediction { get; set; }

	public bool IsMultiplayer => Engine.MaxClients != 1;

	public Vector3 LastOrigin => _previousOrigin;

	public void LoadViewModel( string viewModel, Player player )
	{
		player.ViewModelIndex = Engine.LoadModel( viewModel );
	}

	private T PrepEntity<T>( Player weaponOwner ) where T : BaseEntity, new()
	{
		var entity = _weaponObjects.AddComponent<T>();

		entity.ClientSide = true;

		entity.Precache();
		entity.Spawn();

		if ( weaponOwner is not null && entity is BasePlayerWeapon weapon )
		{
			weapon.Player = weaponOwner;

			weapon.GetItemInfo( out var info );

			_weapons[info.Id] = weapon;
		}

		return entity;
	}

	private void InitClientWeapons()
	{
		if ( _weaponObjects.IsValid() )
			return;

		_weaponObjects = new GameObject( true, "client weapons" );
		_weaponObjects.NetworkMode = NetworkMode.Never;

		Array.Clear( _weapons );

		Globals.Time = (float)Engine.ClientTime;

		_player = PrepEntity<Player>( null );

		_glock = PrepEntity<Glock>( _player );
		_crowbar = PrepEntity<Crowbar>( _player );
		_python = PrepEntity<Python>( _player );
		_mp5 = PrepEntity<Mp5>( _player );
		_crossbow = PrepEntity<Crossbow>( _player );
		_shotgun = PrepEntity<Shotgun>( _player );
		_rpg = PrepEntity<Rpg>( _player );
		_gauss = PrepEntity<Gauss>( _player );
		_egon = PrepEntity<Egon>( _player );
		_hornetGun = PrepEntity<HornetGun>( _player );
		_handGrenade = PrepEntity<HandGrenade>( _player );
		_satchel = PrepEntity<Satchel>( _player );
		_tripmine = PrepEntity<Tripmine>( _player );
		_snark = PrepEntity<Squeak>( _player );
	}

	public void SendWeaponAnim( int animation, int body, bool force )
	{
		if ( !_runFuncs && !force )
			return;

		_currentAnim = animation;

		Engine.WeaponAnimation( animation, body );
	}

	public void PlaySound( string sound, float volume )
	{
		if ( !_runFuncs || _finalState is null )
			return;

		Engine.PlaySoundByNameAtLocation( sound, volume, _finalState.PlayerState.Origin );
	}

	public void PlaybackEvent( EventFlags flags, int eventIndex, float delay, float fparam1, float fparam2, int iparam1, int iparam2, int bparam1, int bparam2 )
	{
		if ( !_runFuncs || _finalState is null )
			return;

		Engine.PlaybackEvent( flags, eventIndex, delay, _finalState.PlayerState.Origin, LastViewAngles, fparam1, fparam2, iparam1, iparam2, bparam1, bparam2 );
	}

	private BasePlayerWeapon PredictedWeapon( int id )
	{
		return id switch
		{
			HalfLife.Crowbar.WeaponId => _crowbar,
			HalfLife.Glock.WeaponId => _glock,
			HalfLife.Python.WeaponId => _python,
			HalfLife.Mp5.WeaponId => _mp5,
			HalfLife.Crossbow.WeaponId => _crossbow,
			HalfLife.Shotgun.WeaponId => _shotgun,
			HalfLife.Rpg.WeaponId => _rpg,
			HalfLife.Gauss.WeaponId => _gauss,
			HalfLife.Egon.WeaponId => _egon,
			HalfLife.HornetGun.WeaponId => _hornetGun,
			HalfLife.HandGrenade.WeaponId => _handGrenade,
			HalfLife.Satchel.WeaponId => _satchel,
			HalfLife.Tripmine.WeaponId => _tripmine,
			HalfLife.Squeak.WeaponId => _snark,
			_ => null
		};
	}

	private static float Decrement( float value, int msec, double minimum )
	{
		value = (float)(value - (msec / 1000.0));

		return value < minimum ? (float)minimum : value;
	}

	private void WeaponsPostThink( LocalState from, LocalState to, ref UserCmd cmd, double time, uint randomSeed )
	{
		InitClientWeapons();

		Globals.Time = (float)time;

		var player = _player;
		var weapon = PredictedWeapon( from.Client.Id );

		_finalState = to;

		if ( _runFuncs )
		{
			if ( to.Client.Health <= 0f && _lastHealth > 0 )
				player.Killed( null, 0 );
			else if ( to.Client.Health > 0f && _lastHealth <= 0 )
				player.Spawn();

			_lastHealth = (int)to.Client.Health;
		}

		if ( weapon is null )
			return;

		for ( var i = 0; i < BasePlayerItem.MaxWeapons; i++ )
		{
			if ( _weapons[i] is not { } current )
				continue;

			ref readonly var data = ref from.WeaponData[i];

			current.InReload = data.InReload != 0;
			current.InSpecialReload = data.InSpecialReload;
			current.Clip = data.Clip;
			current.NextPrimaryAttack = data.NextPrimaryAttack;
			current.NextSecondaryAttack = data.NextSecondaryAttack;
			current.TimeWeaponIdle = data.TimeWeaponIdle;
			current.FUser1 = data.FUser1;
			current.StartThrow = data.FUser2;
			current.ReleaseThrow = data.FUser3;
			current.ChargeReady = data.IUser1;
			current.InAttack = data.IUser2;
			current.FireState = data.IUser3;

			current.SecondaryAmmoType = (int)from.Client.VUser3.z;
			current.PrimaryAmmoType = (int)from.Client.VUser4.x;

			player.SetAmmoSlot( current.PrimaryAmmoType, (int)from.Client.VUser4.y );
			player.SetAmmoSlot( current.SecondaryAmmoType, (int)from.Client.VUser4.z );
		}

		player.RandomSeed = (int)randomSeed;

		player.ButtonLast = from.PlayerState.OldButtons;

		var buttonsChanged = player.ButtonLast ^ cmd.Buttons;

		player.ButtonPressed = buttonsChanged & cmd.Buttons;
		player.ButtonReleased = buttonsChanged & ~cmd.Buttons;
		player.ViewAngle = cmd.ViewAngles;
		player.Origin = from.Client.Origin;

		player.Button = cmd.Buttons;

		player.Velocity = from.Client.Velocity;
		player.EntityFlags = (EntityFlags)from.Client.Flags;

		player.DeadFlag = from.Client.DeadFlag;
		player.WaterLevel = from.Client.WaterLevel;
		player.MaxSpeed = from.Client.MaxSpeed;
		player.Fov = from.Client.Fov;
		player.WeaponAnim = from.Client.WeaponAnim;
		player.ViewModelIndex = from.Client.ViewModel;
		player.NextAttack = from.Client.NextAttack;
		player.NextAmmoBurn = from.Client.FUser2;
		player.AmmoStartCharge = from.Client.FUser3;

		player.Ammo9mm = (int)from.Client.VUser1.x;
		player.Ammo357 = (int)from.Client.VUser1.y;
		player.AmmoArGrenades = (int)from.Client.VUser1.z;
		player.AmmoBolts = from.Client.AmmoNails;
		player.AmmoBuckshot = from.Client.AmmoShells;
		player.AmmoUranium = from.Client.AmmoCells;
		player.AmmoHornets = (int)from.Client.VUser2.x;
		player.AmmoRockets = from.Client.AmmoRockets;

		if ( from.Client.Id != 0 )
			player.ActiveItem = _weapons[from.Client.Id];

		if ( player.ActiveItem.Id == Rpg.WeaponId )
		{
			var rpg = (Rpg)player.ActiveItem;

			rpg.SpotActive = (int)from.Client.VUser2.y;
			rpg.ActiveRockets = (int)from.Client.VUser2.z;
		}

		if ( player.DeadFlag != DeadDiscardBody + 1 && _hud.Health.Health > 0 && player.ViewModelIndex != 0 && User1 == 0 )
		{
			if ( player.NextAttack <= 0f )
				weapon.ItemPostFrame();
		}

		to.Client.Id = from.Client.Id;

		if ( cmd.WeaponSelect != 0 && player.DeadFlag != DeadDiscardBody + 1 )
		{
			if ( cmd.WeaponSelect < from.WeaponData.Length && from.WeaponData[cmd.WeaponSelect].Id == cmd.WeaponSelect )
			{
				var selected = _weapons[cmd.WeaponSelect];

				if ( selected is not null && selected != weapon )
				{
					player.ActiveItem?.Holster();

					player.SetLastItem( player.ActiveItem );
					player.ActiveItem = selected;

					player.ActiveItem?.Deploy();

					to.Client.Id = cmd.WeaponSelect;
				}
			}
		}

		to.Client.ViewModel = player.ViewModelIndex;
		to.Client.Fov = player.Fov;
		to.Client.WeaponAnim = player.WeaponAnim;
		to.Client.NextAttack = player.NextAttack;
		to.Client.FUser2 = player.NextAmmoBurn;
		to.Client.FUser3 = player.AmmoStartCharge;
		to.Client.MaxSpeed = player.MaxSpeed;

		to.Client.VUser1 = new Vector3( player.Ammo9mm, player.Ammo357, player.AmmoArGrenades );

		to.Client.AmmoNails = player.AmmoBolts;
		to.Client.AmmoShells = player.AmmoBuckshot;
		to.Client.AmmoCells = player.AmmoUranium;
		to.Client.VUser2.x = player.AmmoHornets;
		to.Client.AmmoRockets = player.AmmoRockets;

		if ( player.ActiveItem.Id == Rpg.WeaponId )
		{
			var rpg = (Rpg)player.ActiveItem;

			from.Client.VUser2.y = rpg.SpotActive;
			from.Client.VUser2.z = rpg.ActiveRockets;
		}

		if ( _runFuncs && _currentAnim != to.Client.WeaponAnim )
		{
			var body = 2;

			if ( weapon == _tripmine )
				body = 0;

			if ( weapon == _python && IsMultiplayer )
				body = 1;

			SendWeaponAnim( to.Client.WeaponAnim, body, true );
		}

		for ( var i = 0; i < BasePlayerItem.MaxWeapons; i++ )
		{
			ref var data = ref to.WeaponData[i];

			if ( _weapons[i] is not { } current )
			{
				data = default;
				continue;
			}

			data.InReload = current.InReload ? 1 : 0;
			data.InSpecialReload = current.InSpecialReload;
			data.Clip = current.Clip;
			data.NextPrimaryAttack = current.NextPrimaryAttack;
			data.NextSecondaryAttack = current.NextSecondaryAttack;
			data.TimeWeaponIdle = current.TimeWeaponIdle;
			data.FUser1 = current.FUser1;
			data.FUser2 = current.StartThrow;
			data.FUser3 = current.ReleaseThrow;
			data.IUser1 = current.ChargeReady;
			data.IUser2 = current.InAttack;
			data.IUser3 = current.FireState;

			data.NextReload = (float)(data.NextReload - (cmd.Msec / 1000.0));
			data.NextAimBonus = (float)(data.NextAimBonus - (cmd.Msec / 1000.0));
			data.NextPrimaryAttack = (float)(data.NextPrimaryAttack - (cmd.Msec / 1000.0));
			data.NextSecondaryAttack = (float)(data.NextSecondaryAttack - (cmd.Msec / 1000.0));
			data.TimeWeaponIdle = (float)(data.TimeWeaponIdle - (cmd.Msec / 1000.0));
			data.FUser1 = (float)(data.FUser1 - (cmd.Msec / 1000.0));

			to.Client.VUser3.z = current.SecondaryAmmoType;
			to.Client.VUser4 = new Vector3( current.PrimaryAmmoType, player.AmmoSlot( current.PrimaryAmmoType ), player.AmmoSlot( current.SecondaryAmmoType ) );

			if ( data.NextAimBonus < -1.0 )
				data.NextAimBonus = -1f;

			if ( data.NextPrimaryAttack < -1.0 )
				data.NextPrimaryAttack = -1f;

			if ( data.NextSecondaryAttack < -0.001 )
				data.NextSecondaryAttack = -0.001f;

			if ( data.TimeWeaponIdle < -0.001 )
				data.TimeWeaponIdle = -0.001f;

			if ( data.NextReload < -0.001 )
				data.NextReload = -0.001f;

			if ( data.FUser1 < -0.001 )
				data.FUser1 = -0.001f;
		}

		to.Client.NextAttack = Decrement( to.Client.NextAttack, cmd.Msec, -0.001 );
		to.Client.FUser2 = Decrement( to.Client.FUser2, cmd.Msec, -0.001 );
		to.Client.FUser3 = Decrement( to.Client.FUser3, cmd.Msec, -0.001 );

		_previousOrigin = _finalState.PlayerState.Origin + _finalState.Client.ViewOffset;

		_finalState = null;
	}
}
