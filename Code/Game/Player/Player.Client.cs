using System;
using GoldSrc;

namespace HalfLife;

partial class Player
{
	private const int MaxIdRange = 2048;
	private const int StatusBarTargetName = 1;
	private const int StatusBarTargetHealth = 2;
	private const int StatusBarTargetArmor = 3;
	private const int StatusBarEnd = 4;

	private readonly int[] _statusBarState = new int[StatusBarEnd];

	private float _nextStatusBarUpdateTime;
	private float _statusBarDisappearDelay;
	private string _statusBarString0 = "";
	private string _statusBarString1 = "";

	private BaseEntity _observerTarget;
	private float _nextObserverInput;
	private int _observerWeapon;
	private int _observerLastMode;

	internal BaseEntity ObserverTarget
	{
		get => _observerTarget.IsValid() ? _observerTarget : null;
		set => _observerTarget = value;
	}

	public void ForceClientDllUpdate()
	{
		_clientHealth = -1;
		_clientBattery = -1;
		Train |= TrainNew;
		WeaponSent = false;
		_knownItem = false;
		_initHud = true;

		UpdateClientData();
	}

	public virtual void UpdateClientData()
	{
		if ( _initHud )
		{
			_initHud = false;
			HalfLifeDll.InitHud = false;

			Server.MessageBegin( MessageDest.One, UserMessages.ResetHud, default, this );
			Server.WriteByte( 0 );
			Server.MessageEnd();

			if ( !_gameHudInitialized )
			{
				Server.MessageBegin( MessageDest.One, UserMessages.InitHud, default, this );
				Server.MessageEnd();

				HalfLifeDll.GameRules.InitHud( this );
				_gameHudInitialized = true;

				_observerLastMode = ObserverRoaming;

				if ( HalfLifeDll.GameRules.IsMultiplayer() )
					FireTargets( "game_playerjoin", this, this, UseType.Toggle, 0f );
			}

			FireTargets( "game_playerspawn", this, this, UseType.Toggle, 0f );

			InitStatusBar();

			Server.MessageBegin( MessageDest.One, UserMessages.Flashlight, default, this );
			Server.WriteByte( FlashlightIsOn ? 1 : 0 );
			Server.WriteByte( _flashBattery );
			Server.MessageEnd();
		}

		if ( HideHud != _clientHideHud )
		{
			Server.MessageBegin( MessageDest.One, UserMessages.HideWeapon, default, this );
			Server.WriteByte( HideHud );
			Server.MessageEnd();

			_clientHideHud = HideHud;
		}

		if ( FieldOfViewSetting != ClientFieldOfView )
		{
			Server.MessageBegin( MessageDest.One, UserMessages.SetFov, default, this );
			Server.WriteByte( FieldOfViewSetting );
			Server.MessageEnd();
		}

		if ( HalfLifeDll.DisplayTitle )
		{
			Server.MessageBegin( MessageDest.One, UserMessages.ShowGameTitle, default, this );
			Server.WriteByte( 0 );
			Server.MessageEnd();

			HalfLifeDll.DisplayTitle = false;
		}

		if ( Health != _clientHealth )
		{
			var health = (int)Math.Clamp( Health, 0f, 255f );

			if ( Health > 0f && Health <= 1f )
				health = 1;

			Server.MessageBegin( MessageDest.One, UserMessages.Health, default, this );
			Server.WriteByte( health );
			Server.MessageEnd();

			_clientHealth = (int)Health;
		}

		if ( ArmorValue != _clientBattery )
		{
			_clientBattery = (int)ArmorValue;

			Server.MessageBegin( MessageDest.One, UserMessages.Battery, default, this );
			Server.WriteShort( (int)ArmorValue );
			Server.MessageEnd();
		}

		if ( DamageTake != 0f || DamageSave != 0f || _hudDamageBits != DamageTypeBits )
		{
			var damageOrigin = DamageInflictor is BaseEntity inflictor ? inflictor.Center : Origin;
			var visibleDamageBits = DamageTypeBits & DamageShownHud;

			Server.MessageBegin( MessageDest.One, UserMessages.Damage, default, this );
			Server.WriteByte( (int)DamageSave );
			Server.WriteByte( (int)DamageTake );
			Server.WriteLong( visibleDamageBits );
			Server.WriteCoord( damageOrigin.x );
			Server.WriteCoord( damageOrigin.y );
			Server.WriteCoord( damageOrigin.z );
			Server.MessageEnd();

			DamageTake = 0f;
			DamageSave = 0f;
			_hudDamageBits = DamageTypeBits;

			DamageTypeBits &= DamageTimeBased;
		}

		if ( _flashLightTime != 0f && _flashLightTime <= Server.Globals.Time )
		{
			if ( FlashlightIsOn )
			{
				if ( _flashBattery != 0 )
				{
					_flashLightTime = (float)(FlashDrainTime + Server.Globals.Time);
					_flashBattery--;

					if ( _flashBattery == 0 )
						FlashlightTurnOff();
				}
			}
			else if ( _flashBattery < 100 )
			{
				_flashLightTime = (float)(FlashChargeTime + Server.Globals.Time);
				_flashBattery++;
			}
			else
			{
				_flashLightTime = 0f;
			}

			Server.MessageBegin( MessageDest.One, UserMessages.FlashBattery, default, this );
			Server.WriteByte( _flashBattery );
			Server.MessageEnd();
		}

		if ( (Train & TrainNew) != 0 )
		{
			Server.MessageBegin( MessageDest.One, UserMessages.Train, default, this );
			Server.WriteByte( Train & 0xF );
			Server.MessageEnd();

			Train &= ~TrainNew;
		}

		if ( !_knownItem )
		{
			_knownItem = true;

			foreach ( var info in BasePlayerItem.ItemInfoArray )
			{
				if ( info.Id == 0 )
					continue;

				Server.MessageBegin( MessageDest.One, UserMessages.WeaponList, default, this );
				Server.WriteString( info.Name ?? "Empty" );
				Server.WriteByte( GetAmmoIndex( info.Ammo1 ) );
				Server.WriteByte( info.MaxAmmo1 );
				Server.WriteByte( GetAmmoIndex( info.Ammo2 ) );
				Server.WriteByte( info.MaxAmmo2 );
				Server.WriteByte( info.Slot );
				Server.WriteByte( info.Position );
				Server.WriteByte( info.Id );
				Server.WriteByte( (int)info.Flags );
				Server.MessageEnd();
			}
		}

		SendAmmoUpdate();

		for ( var i = 0; i < BasePlayerItem.MaxItemTypes; i++ )
			PlayerItems[i]?.UpdateClientData( this );

		ClientActiveItem = ActiveItem;
		ClientFieldOfView = FieldOfViewSetting;

		if ( _nextStatusBarUpdateTime < Server.Globals.Time )
		{
			UpdateStatusBar();
			_nextStatusBarUpdateTime = (float)(Server.Globals.Time + 0.2);
		}
	}

	public void InitStatusBar()
	{
		_statusBarDisappearDelay = 0f;
		_statusBarString1 = _statusBarString0 = "";
	}

	public void UpdateStatusBar()
	{
		var newState = new int[StatusBarEnd];
		var string0 = _statusBarString0;
		var string1 = _statusBarString1;

		Server.Globals.MakeVectors( ViewAngle + PunchAngle );

		var source = EyePosition;
		var end = source + (Server.Globals.Forward * MaxIdRange);
		var trace = Util.TraceLine( source, end, DontIgnoreMonsters, this );

		if ( trace.Fraction != 1f )
		{
			if ( !Util.IsNullEnt( trace.Ent ) )
			{
				var entity = (BaseEntity)trace.Ent;

				if ( entity.Classify() == ClassPlayer )
				{
					newState[StatusBarTargetName] = entity.Index;
					string1 = "1 %p1\n2 Health: %i2%%\n3 Armor: %i3%%";

					if ( HalfLifeDll.GameRules.PlayerRelationship( this, entity ) == HalfLife.PlayerRelationship.Teammate )
					{
						newState[StatusBarTargetHealth] = (int)(100 * (entity.Health / entity.MaxHealth));
						newState[StatusBarTargetArmor] = (int)entity.ArmorValue;
					}

					_statusBarDisappearDelay = (float)(Server.Globals.Time + 1.0);
				}
			}
			else if ( _statusBarDisappearDelay > Server.Globals.Time )
			{
				newState[StatusBarTargetName] = _statusBarState[StatusBarTargetName];
				newState[StatusBarTargetHealth] = _statusBarState[StatusBarTargetHealth];
				newState[StatusBarTargetArmor] = _statusBarState[StatusBarTargetArmor];
			}
		}

		var forceResend = false;

		if ( string0 != _statusBarString0 )
		{
			Server.MessageBegin( MessageDest.One, UserMessages.StatusText, default, this );
			Server.WriteByte( 0 );
			Server.WriteString( string0 );
			Server.MessageEnd();

			_statusBarString0 = string0;
			forceResend = true;
		}

		if ( string1 != _statusBarString1 )
		{
			Server.MessageBegin( MessageDest.One, UserMessages.StatusText, default, this );
			Server.WriteByte( 1 );
			Server.WriteString( string1 );
			Server.MessageEnd();

			_statusBarString1 = string1;
			forceResend = true;
		}

		for ( var i = 1; i < StatusBarEnd; i++ )
		{
			if ( newState[i] == _statusBarState[i] && !forceResend )
				continue;

			Server.MessageBegin( MessageDest.One, UserMessages.StatusValue, default, this );
			Server.WriteByte( i );
			Server.WriteShort( newState[i] );
			Server.MessageEnd();

			_statusBarState[i] = newState[i];
		}
	}

	public virtual void ImpulseCommands()
	{
		PlayerUse();

		switch ( Impulse )
		{
			case 99:
				var on = UserMessages.Logo == 0;

				if ( on )
					UserMessages.Logo = Server.RegUserMsg( "Logo", 1 );

				Server.MessageBegin( MessageDest.One, UserMessages.Logo, default, this );
				Server.WriteByte( on ? 1 : 0 );
				Server.MessageEnd();

				if ( !on )
					UserMessages.Logo = 0;

				break;

			case 100:
				if ( FlashlightIsOn )
					FlashlightTurnOff();
				else
					FlashlightTurnOn();

				break;

			case 201:
				if ( Server.Globals.Time < NextDecalTime )
					break;

				Server.Globals.MakeVectors( ViewAngle );

				var trace = Util.TraceLine( Origin + ViewOffset, Origin + ViewOffset + (Server.Globals.Forward * 128f), IgnoreMonsters, this );

				if ( trace.Fraction != 1f )
				{
					NextDecalTime = Server.Globals.Time + Cvar.GetFloat( "decalfrequency" );
					Server.CreateEntity<SprayCan>().Spawn( this );
				}

				break;

			default:
				CheatImpulseCommands( Impulse );
				break;
		}

		Impulse = 0;
	}

	public void CheatImpulseCommands( int impulse )
	{
		if ( !Sandbox.Game.CheatsEnabled )
			return;

		BaseEntity entity;

		switch ( impulse )
		{
			case 76:
				if ( !HalfLifeDll.PrecacheGrunt )
				{
					HalfLifeDll.PrecacheGrunt = true;
					Util.Alert( AlertType.Console, "You must now restart to use Grunt-o-matic.\n" );
				}
				else
				{
					Server.Globals.MakeVectors( new Vector3( 0f, ViewAngle.y, 0f ) );
					Create( "monster_human_grunt", Origin + (Server.Globals.Forward * 128f), Angles, null );
				}

				break;

			case 101:
				HalfLifeDll.EvilImpulse101 = true;

				GiveNamedItem( "item_suit" );
				GiveNamedItem( "item_battery" );
				GiveNamedItem( "weapon_crowbar" );
				GiveNamedItem( "weapon_9mmhandgun" );
				GiveNamedItem( "ammo_9mmclip" );
				GiveNamedItem( "weapon_shotgun" );
				GiveNamedItem( "ammo_buckshot" );
				GiveNamedItem( "weapon_9mmAR" );
				GiveNamedItem( "ammo_9mmAR" );
				GiveNamedItem( "ammo_ARgrenades" );
				GiveNamedItem( "weapon_handgrenade" );
				GiveNamedItem( "weapon_tripmine" );
				GiveNamedItem( "weapon_357" );
				GiveNamedItem( "ammo_357" );
				GiveNamedItem( "weapon_crossbow" );
				GiveNamedItem( "ammo_crossbow" );
				GiveNamedItem( "weapon_egon" );
				GiveNamedItem( "weapon_gauss" );
				GiveNamedItem( "ammo_gaussclip" );
				GiveNamedItem( "weapon_rpg" );
				GiveNamedItem( "ammo_rpgclip" );
				GiveNamedItem( "weapon_satchel" );
				GiveNamedItem( "weapon_snark" );
				GiveNamedItem( "weapon_hornetgun" );

				HalfLifeDll.EvilImpulse101 = false;
				break;

			case 102:
				Gib.SpawnRandomGibs( this, 1, true );
				break;

			case 103:
				(FindEntityForward() as BaseMonster)?.ReportAiState();
				break;

			case 104:
				HalfLifeDll.GlobalState.DumpGlobals();
				break;

			case 105:
				if ( _noPlayerSound )
				{
					Util.Alert( AlertType.Console, "Player is audible\n" );
					_noPlayerSound = false;
				}
				else
				{
					Util.Alert( AlertType.Console, "Player is silent\n" );
					_noPlayerSound = true;
				}

				break;

			case 106:
				entity = FindEntityForward();

				if ( entity is not null )
				{
					Util.Alert( AlertType.Console, $"Classname: {entity.ClassName}" );

					if ( !string.IsNullOrEmpty( entity.TargetName ) )
						Util.Alert( AlertType.Console, $" - Targetname: {entity.TargetName}\n" );
					else
						Util.Alert( AlertType.Console, " - TargetName: No Targetname\n" );

					Util.Alert( AlertType.Console, $"Model: {entity.ModelName}\n" );

					if ( !string.IsNullOrEmpty( entity.GlobalName ) )
						Util.Alert( AlertType.Console, $"Globalname: {entity.GlobalName}\n" );
				}

				break;

			case 107:
				var start = Origin + ViewOffset;
				var end = start + (Server.Globals.Forward * 1024f);
				var textureTrace = Util.TraceLine( start, end, IgnoreMonsters, this );

				if ( Server.TraceTexture( textureTrace.Ent ?? Server.GetEdict( 0 ), start, end ) is { } textureName )
					Util.Alert( AlertType.Console, $"Texture: {textureName}\n" );

				break;

			case 195:
				Create( "node_viewer_fly", Origin, Angles, null );
				break;

			case 196:
				Create( "node_viewer_large", Origin, Angles, null );
				break;

			case 197:
				Create( "node_viewer_human", Origin, Angles, null );
				break;

			case 199:
				Util.Alert( AlertType.Console, $"{HalfLifeDll.WorldGraph.FindNearestNode( Origin, GraphNode.GroupRealm )}\n" );
				HalfLifeDll.WorldGraph.ShowNodeConnections( HalfLifeDll.WorldGraph.FindNearestNode( Origin, GraphNode.GroupRealm ) );
				break;

			case 202:
				Server.Globals.MakeVectors( ViewAngle );

				var bloodTrace = Util.TraceLine( Origin + ViewOffset, Origin + ViewOffset + (Server.Globals.Forward * 128f), IgnoreMonsters, this );

				if ( bloodTrace.Fraction != 1f )
					Server.CreateEntity<BloodSplat>().Spawn( this );

				break;

			case 203:
				entity = FindEntityForward();

				if ( entity is not null && entity.DamageMode != DamageNo )
					entity.SetThink( entity.SubRemove );

				break;
		}
	}

	private BaseEntity FindEntityForward()
	{
		Server.Globals.MakeVectors( ViewAngle );

		var trace = Util.TraceLine( Origin + ViewOffset, Origin + ViewOffset + (Server.Globals.Forward * 8192f), DontIgnoreMonsters, this );

		return trace.Fraction != 1f && !Util.IsNullEnt( trace.Ent ) ? trace.Ent as BaseEntity : null;
	}

	public void ObserverFindNextPlayer( bool reverse )
	{
		var start = ObserverTarget?.Index ?? Index;
		var current = start;
		var direction = reverse ? -1 : 1;

		ObserverTarget = null;

		do
		{
			current += direction;

			if ( current > Server.MaxClients )
				current = 1;

			if ( current < 1 )
				current = Server.MaxClients;

			if ( Util.PlayerByIndex( current ) is not Player player || player == this )
				continue;

			if ( player.IsObserver || (player.Effects & Effects.NoDraw) == Effects.NoDraw )
				continue;

			ObserverTarget = player;
			break;
		}
		while ( current != start );

		if ( ObserverTarget is { } target )
		{
			SetOrigin( target.Origin );

			if ( IUser1 != ObserverRoaming )
				IUser2 = target.Index;
		}
	}

	private void ObserverHandleButtons()
	{
		if ( _nextObserverInput > Server.Globals.Time )
			return;

		if ( (ButtonPressed & (int)InButtons.Jump) != 0 )
		{
			ObserverSetMode( IUser1 switch
			{
				ObserverChaseLocked => ObserverChaseFree,
				ObserverChaseFree => ObserverInEye,
				ObserverInEye => ObserverRoaming,
				ObserverRoaming => ObserverMapFree,
				ObserverMapFree => ObserverMapChase,
				_ => ObserverChaseFree
			} );

			_nextObserverInput = (float)(Server.Globals.Time + 0.2);
		}

		if ( (ButtonPressed & (int)InButtons.Attack) != 0 )
		{
			ObserverFindNextPlayer( false );
			_nextObserverInput = (float)(Server.Globals.Time + 0.2);
		}

		if ( (ButtonPressed & (int)InButtons.Attack2) != 0 )
		{
			ObserverFindNextPlayer( true );
			_nextObserverInput = (float)(Server.Globals.Time + 0.2);
		}
	}

	private void ObserverCheckTarget()
	{
		if ( IUser1 == ObserverRoaming )
			return;

		if ( ObserverTarget is null )
		{
			ObserverFindNextPlayer( false );

			if ( ObserverTarget is null )
			{
				var lastMode = IUser1;

				ObserverSetMode( ObserverRoaming );
				_observerLastMode = lastMode;

				return;
			}
		}

		if ( Util.PlayerByIndex( ObserverTarget.Index ) is not Player target )
		{
			ObserverFindNextPlayer( false );
			return;
		}

		if ( target.DeadFlag == DeadDead && target.DeadTime + 2f < Server.Globals.Time )
			ObserverFindNextPlayer( false );
	}

	private void ObserverCheckProperties()
	{
		if ( IUser1 == ObserverInEye && ObserverTarget is not null )
		{
			if ( Util.PlayerByIndex( ObserverTarget.Index ) is not Player target )
				return;

			var weapon = target.ActiveItem?.Id ?? 0;

			if ( FieldOfViewSetting == target.FieldOfViewSetting && _observerWeapon == weapon )
				return;

			FieldOfViewSetting = target.FieldOfViewSetting;
			ClientFieldOfView = FieldOfViewSetting;

			Server.MessageBegin( MessageDest.One, UserMessages.SetFov, default, this );
			Server.WriteByte( FieldOfViewSetting );
			Server.MessageEnd();

			_observerWeapon = weapon;

			Server.MessageBegin( MessageDest.One, UserMessages.CurWeapon, default, this );
			Server.WriteByte( 1 );
			Server.WriteByte( _observerWeapon );
			Server.WriteByte( 0 );
			Server.MessageEnd();

			return;
		}

		FieldOfViewSetting = 90;

		if ( _observerWeapon == 0 )
			return;

		_observerWeapon = 0;

		Server.MessageBegin( MessageDest.One, UserMessages.CurWeapon, default, this );
		Server.WriteByte( 1 );
		Server.WriteByte( _observerWeapon );
		Server.WriteByte( 0 );
		Server.MessageEnd();
	}

	public void ObserverSetMode( int mode )
	{
		if ( mode == IUser1 )
			return;

		if ( mode < ObserverChaseLocked || mode > ObserverMapChase )
			mode = ObserverInEye;

		if ( ObserverTarget is { } current )
		{
			if ( current == this || (current is Player player && (player.IsObserver || (player.Effects & Effects.NoDraw) == Effects.NoDraw)) )
				ObserverTarget = null;
		}

		IUser1 = mode;

		if ( mode != ObserverRoaming && ObserverTarget is null )
		{
			ObserverFindNextPlayer( false );

			if ( ObserverTarget is null )
			{
				Util.ClientPrint( this, UserMessages.HudPrintCenter, "#Spec_NoTarget" );
				IUser1 = ObserverRoaming;
			}
		}

		IUser2 = IUser1 == ObserverRoaming ? 0 : ObserverTarget.Index;
		IUser3 = 0;

		Util.ClientPrint( this, UserMessages.HudPrintCenter, $"#Spec_Mode{IUser1}" );

		_observerLastMode = mode;
	}
}

public class SprayCan : BaseEntity
{
	public override ObjectCaps ObjectCaps => ObjectCaps.DontSave;

	public void Spawn( Player owner )
	{
		Origin = owner.Origin + new Vector3( 0f, 0f, 32f );
		Angles = owner.ViewAngle;
		Owner = owner;
		Frame = 0f;

		NextThink = (float)(Server.Globals.Time + 0.1);

		Sound.Emit( this, SoundChannel.Voice, "player/sprayer.wav", 1f, Server.AttenuationNormal );
	}

	public override void Think()
	{
		var frames = Owner is Player player ? player.GetCustomDecalFrames() : -1;
		var playerNumber = Owner?.Index ?? 0;

		Server.Globals.MakeVectors( Angles );

		var trace = Util.TraceLine( Origin, Origin + (Server.Globals.Forward * 128f), IgnoreMonsters, Owner );

		if ( frames == -1 )
		{
			Util.DecalTrace( trace, Decals.Lambda6 );
			Remove();
		}
		else
		{
			Util.PlayerDecalTrace( trace, playerNumber, (int)Frame, true );

			if ( Frame++ >= frames - 1 )
				Remove();
		}

		NextThink = (float)(Server.Globals.Time + 0.1);
	}
}

public class BloodSplat : BaseEntity
{
	public void Spawn( Player owner )
	{
		Origin = owner.Origin + new Vector3( 0f, 0f, 32f );
		Angles = owner.ViewAngle;
		Owner = owner;

		SetThink( Spray );
		NextThink = (float)(Server.Globals.Time + 0.1);
	}

	private void Spray()
	{
		if ( HalfLifeDll.Language != HalfLifeDll.LanguageGerman )
		{
			Server.Globals.MakeVectors( Angles );

			var trace = Util.TraceLine( Origin, Origin + (Server.Globals.Forward * 128f), IgnoreMonsters, Owner );

			Util.BloodDecalTrace( trace, BloodColorRed );
		}

		SetThink( SubRemove );
		NextThink = (float)(Server.Globals.Time + 0.1);
	}
}

[LinkEntityToClass( "player_weaponstrip" )]
public class StripWeapons : PointEntity
{
	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		Player player = null;

		if ( activator is { IsPlayer: true } )
			player = activator as Player;
		else if ( !HalfLifeDll.GameRules.IsDeathmatch() )
			player = Server.EntityOfEntIndex( 1 ) as Player;

		player?.RemoveAllItems( false );
	}
}

[LinkEntityToClass( "player_loadsaved" )]
public class RevertSaved : PointEntity
{
	[Save( FieldType.Float )]
	private float _messageTime;
	[Save( FieldType.Float )]
	private float _loadTime;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "duration":
				DamageTake = Util.Atof( value );
				return true;
			case "holdtime":
				DamageSave = Util.Atof( value );
				return true;
			case "messagetime":
				_messageTime = Util.Atof( value );
				return true;
			case "loadtime":
				_loadTime = Util.Atof( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		Util.ScreenFadeAll( RenderColor, DamageTake, DamageSave, (int)RenderAmt, Util.FadeOut );

		NextThink = Server.Globals.Time + _messageTime;
		SetThink( MessageThink );
	}

	private void MessageThink()
	{
		Util.ShowMessageAll( Message );

		var nextThink = _loadTime - _messageTime;

		if ( nextThink > 0f )
		{
			NextThink = Server.Globals.Time + nextThink;
			SetThink( LoadThink );
		}
		else
		{
			LoadThink();
		}
	}

	private void LoadThink()
	{
		if ( !Server.Deathmatch )
			Server.ServerCommand( "reload\n" );
	}
}

[LinkEntityToClass( "info_intermission" )]
public class InfoIntermission : PointEntity
{
	public override void Spawn()
	{
		SetOrigin( Origin );

		Solid = Solid.Not;
		Effects = Effects.NoDraw;
		ViewAngle = Vector3.Zero;

		NextThink = Server.Globals.Time + 2f;
	}

	public override void Think()
	{
		var target = Server.FindEntityByString( null, "targetname", Target );

		if ( Util.IsNullEnt( target ) )
			return;

		var angles = MathLib.VectorAngles( Util.Normalize( target.Origin - Origin ) );

		ViewAngle = new Vector3( -angles.x, angles.y, angles.z );
	}
}

[LinkEntityToClass( "monster_hevsuit_dead" )]
public class DeadHev : BaseMonster
{
	private static readonly string[] Poses = ["deadback", "deadsitting", "deadstomach", "deadtable"];

	private int _pose;

	public override int Classify() => ClassHumanMilitary;

	public override bool KeyValue( string key, string value )
	{
		if ( key != "pose" )
			return base.KeyValue( key, value );

		_pose = Util.Atoi( value );
		return true;
	}

	public override void Spawn()
	{
		Server.PrecacheModel( "models/player.mdl" );
		SetModel( "models/player.mdl" );

		Effects = 0;
		YawSpeed = 8f;
		Sequence = 0;
		Body = 1;
		MonsterBloodColor = BloodColorRed;

		Sequence = LookupSequence( Poses[_pose] );

		if ( Sequence == -1 )
		{
			Util.Alert( AlertType.Console, "Dead hevsuit with bad pose\n" );
			Sequence = 0;
			Effects = Effects.BrightField;
		}

		Health = 8f;

		MonsterInitDead();
	}
}
