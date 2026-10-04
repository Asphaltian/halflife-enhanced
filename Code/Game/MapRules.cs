using System;
using GoldSrc;

namespace HalfLife;

public class RuleEntity : BaseEntity
{
	[Save( FieldType.String )]
	private string _master;

	public override void Spawn()
	{
		Solid = Solid.Not;
		MoveType = MoveType.None;
		Effects = Effects.NoDraw;
	}

	public override bool KeyValue( string key, string value )
	{
		if ( key == "master" )
		{
			_master = Server.AllocString( value );
			return true;
		}

		return base.KeyValue( key, value );
	}

	protected bool CanFireForActivator( BaseEntity activator )
	{
		return string.IsNullOrEmpty( _master ) || IsMasterTriggered( _master, activator );
	}
}

public class RulePointEntity : RuleEntity
{
	public override void Spawn()
	{
		base.Spawn();
		Frame = 0f;
		ModelName = null;
	}
}

public class RuleBrushEntity : RuleEntity
{
	public override void Spawn()
	{
		SetModel( ModelName );
		base.Spawn();
	}
}

[LinkEntityToClass( "game_score" )]
public class GameScore : RulePointEntity
{
	private const int SpawnFlagNegative = 0x0001;
	private const int SpawnFlagTeam = 0x0002;

	public override bool KeyValue( string key, string value )
	{
		if ( key == "points" )
		{
			Frags = Util.Atoi( value );
			return true;
		}

		return base.KeyValue( key, value );
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( !CanFireForActivator( activator ) )
			return;

		if ( !activator.IsPlayer )
			return;

		if ( (SpawnFlags & SpawnFlagTeam) != 0 )
			activator.AddPointsToTeam( (int)Frags, (SpawnFlags & SpawnFlagNegative) != 0 );
		else
			activator.AddPoints( (int)Frags, (SpawnFlags & SpawnFlagNegative) != 0 );
	}
}

[LinkEntityToClass( "game_end" )]
public class GameEnd : RulePointEntity
{
	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( !CanFireForActivator( activator ) )
			return;

		HalfLifeDll.GameRules.EndMultiplayerGame();
	}
}

[LinkEntityToClass( "game_text" )]
public class GameText : RulePointEntity
{
	private const int SpawnFlagAllPlayers = 0x0001;

	[Save( FieldType.Float )]
	private float _x;
	[Save( FieldType.Float )]
	private float _y;
	[Save( FieldType.Integer )]
	private int _effect;
	[Save( FieldType.Character )]
	private readonly byte[] _color = new byte[4];
	[Save( FieldType.Character )]
	private readonly byte[] _color2 = new byte[4];
	[Save( FieldType.Float )]
	private float _fadeInTime;
	[Save( FieldType.Float )]
	private float _fadeOutTime;
	[Save( FieldType.Float )]
	private float _holdTime;
	[Save( FieldType.Float )]
	private float _fxTime;
	[Save( FieldType.Integer )]
	private int _channel;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "channel":
				_channel = Util.Atoi( value );
				return true;
			case "x":
				_x = Util.Atof( value );
				return true;
			case "y":
				_y = Util.Atof( value );
				return true;
			case "effect":
				_effect = Util.Atoi( value );
				return true;
			case "color":
				ParseColor( _color, value );
				return true;
			case "color2":
				ParseColor( _color2, value );
				return true;
			case "fadein":
				_fadeInTime = Util.Atof( value );
				return true;
			case "fadeout":
				_fadeOutTime = Util.Atof( value );
				return true;
			case "holdtime":
				_holdTime = Util.Atof( value );
				return true;
			case "fxtime":
				_fxTime = Util.Atof( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	private static void ParseColor( byte[] color, string value )
	{
		var parsed = new int[4];

		Util.StringToIntArray( parsed, value );

		for ( var i = 0; i < 4; i++ )
			color[i] = (byte)parsed[i];
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( !CanFireForActivator( activator ) )
			return;

		var parms = new HudTextParms
		{
			X = _x,
			Y = _y,
			Effect = _effect,
			R1 = _color[0],
			G1 = _color[1],
			B1 = _color[2],
			A1 = _color[3],
			R2 = _color2[0],
			G2 = _color2[1],
			B2 = _color2[2],
			A2 = _color2[3],
			FadeInTime = _fadeInTime,
			FadeOutTime = _fadeOutTime,
			HoldTime = _holdTime,
			FxTime = _fxTime,
			Channel = _channel
		};

		if ( (SpawnFlags & SpawnFlagAllPlayers) != 0 )
			Util.HudMessageAll( parms, Message ?? "" );
		else if ( activator.IsNetClient )
			Util.HudMessage( activator, parms, Message ?? "" );
	}
}

[LinkEntityToClass( "game_team_master" )]
public class GameTeamMaster : RulePointEntity
{
	private const int SpawnFlagFireOnce = 0x0001;
	private const int SpawnFlagAnyTeam = 0x0002;

	private int _teamIndex;
	private UseType _triggerType;

	public override ObjectCaps ObjectCaps => base.ObjectCaps | ObjectCaps.Master;

	public override string TeamId => _teamIndex < 0 ? "" : HalfLifeDll.GameRules.GetIndexedTeamName( _teamIndex );

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "teamindex":
				_teamIndex = Util.Atoi( value );
				return true;
			case "triggerstate":
				_triggerType = Util.Atoi( value ) switch
				{
					0 => UseType.Off,
					2 => UseType.Toggle,
					_ => UseType.On
				};
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( !CanFireForActivator( activator ) )
			return;

		if ( useType == UseType.Set )
		{
			_teamIndex = value < 0f ? -1 : HalfLifeDll.GameRules.GetTeamIndex( activator.TeamId );
			return;
		}

		if ( !TeamMatch( activator ) )
			return;

		UseTargets( activator, _triggerType, value );

		if ( (SpawnFlags & SpawnFlagFireOnce) != 0 )
			Remove();
	}

	public override bool IsTriggered( BaseEntity activator ) => TeamMatch( activator );

	private bool TeamMatch( BaseEntity activator )
	{
		if ( _teamIndex < 0 && (SpawnFlags & SpawnFlagAnyTeam) != 0 )
			return true;

		if ( activator is null )
			return false;

		return Util.TeamsMatch( activator.TeamId, TeamId );
	}
}

[LinkEntityToClass( "game_team_set" )]
public class GameTeamSet : RulePointEntity
{
	private const int SpawnFlagFireOnce = 0x0001;
	private const int SpawnFlagClearTeam = 0x0002;

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( !CanFireForActivator( activator ) )
			return;

		UseTargets( activator, UseType.Set, (SpawnFlags & SpawnFlagClearTeam) != 0 ? -1f : 0f );

		if ( (SpawnFlags & SpawnFlagFireOnce) != 0 )
			Remove();
	}
}

[LinkEntityToClass( "game_zone_player" )]
public class GamePlayerZone : RuleBrushEntity
{
	[Save( FieldType.String )]
	private string _inTarget;
	[Save( FieldType.String )]
	private string _outTarget;
	[Save( FieldType.String )]
	private string _inCount;
	[Save( FieldType.String )]
	private string _outCount;

	public override bool KeyValue( string key, string value )
	{
		switch ( key )
		{
			case "intarget":
				_inTarget = Server.AllocString( value );
				return true;
			case "outtarget":
				_outTarget = Server.AllocString( value );
				return true;
			case "incount":
				_inCount = Server.AllocString( value );
				return true;
			case "outcount":
				_outCount = Server.AllocString( value );
				return true;
			default:
				return base.KeyValue( key, value );
		}
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		var playersInCount = 0;
		var playersOutCount = 0;

		if ( !CanFireForActivator( activator ) )
			return;

		for ( var i = 1; i <= Server.MaxClients; i++ )
		{
			if ( Util.PlayerByIndex( i ) is not { } player )
				continue;

			var hullNumber = (player.EntityFlags & EntityFlags.Ducking) == EntityFlags.Ducking ? HeadHull : HumanHull;
			var trace = Util.TraceModel( player.Origin, player.Origin, hullNumber, this );

			if ( trace.StartSolid )
			{
				playersInCount++;

				if ( !string.IsNullOrEmpty( _inTarget ) )
					FireTargets( _inTarget, player, activator, useType, value );
			}
			else
			{
				playersOutCount++;

				if ( !string.IsNullOrEmpty( _outTarget ) )
					FireTargets( _outTarget, player, activator, useType, value );
			}
		}

		if ( !string.IsNullOrEmpty( _inCount ) )
			FireTargets( _inCount, activator, this, UseType.Set, playersInCount );

		if ( !string.IsNullOrEmpty( _outCount ) )
			FireTargets( _outCount, activator, this, UseType.Set, playersOutCount );
	}
}

[LinkEntityToClass( "game_player_hurt" )]
public class GamePlayerHurt : RulePointEntity
{
	private const int SpawnFlagFireOnce = 0x0001;

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( !CanFireForActivator( activator ) )
			return;

		if ( activator.IsPlayer )
		{
			if ( Damage < 0f )
				activator.TakeHealth( -Damage, DamageGeneric );
			else
				activator.TakeDamage( this, this, Damage, DamageGeneric );
		}

		UseTargets( activator, useType, value );

		if ( (SpawnFlags & SpawnFlagFireOnce) != 0 )
			Remove();
	}
}

[LinkEntityToClass( "game_counter" )]
public class GameCounter : RulePointEntity
{
	private const int SpawnFlagFireOnce = 0x0001;
	private const int SpawnFlagReset = 0x0002;

	public override void Spawn()
	{
		Damage = (int)Frags;
		base.Spawn();
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( !CanFireForActivator( activator ) )
			return;

		switch ( useType )
		{
			case UseType.On:
			case UseType.Toggle:
				Frags++;
				break;
			case UseType.Off:
				Frags--;
				break;
			case UseType.Set:
				Frags = (int)value;
				break;
		}

		if ( (int)Frags != (int)Health )
			return;

		UseTargets( activator, UseType.Toggle, 0f );

		if ( (SpawnFlags & SpawnFlagFireOnce) != 0 )
			Remove();

		if ( (SpawnFlags & SpawnFlagReset) != 0 )
			Frags = Damage;
	}
}

[LinkEntityToClass( "game_counter_set" )]
public class GameCounterSet : RulePointEntity
{
	private const int SpawnFlagFireOnce = 0x0001;

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( !CanFireForActivator( activator ) )
			return;

		UseTargets( activator, UseType.Set, Frags );

		if ( (SpawnFlags & SpawnFlagFireOnce) != 0 )
			Remove();
	}
}

[LinkEntityToClass( "game_player_equip" )]
public class GamePlayerEquip : RulePointEntity
{
	private const int SpawnFlagUseOnly = 0x0001;
	private const int MaxEquip = 32;
	private const int MaxTokenLength = 127;

	private readonly string[] _weaponNames = new string[MaxEquip];
	private readonly int[] _weaponCount = new int[MaxEquip];

	public override bool KeyValue( string key, string value )
	{
		if ( base.KeyValue( key, value ) )
			return true;

		for ( var i = 0; i < MaxEquip; i++ )
		{
			if ( !string.IsNullOrEmpty( _weaponNames[i] ) )
				continue;

			_weaponNames[i] = Server.AllocString( Triggers.StripToken( key, MaxTokenLength ) );
			_weaponCount[i] = Math.Max( 1, Util.Atoi( value ) );
			return true;
		}

		return false;
	}

	public override void Touch( BaseEntity other )
	{
		if ( !CanFireForActivator( other ) )
			return;

		if ( (SpawnFlags & SpawnFlagUseOnly) != 0 )
			return;

		EquipPlayer( other );
	}

	private void EquipPlayer( BaseEntity entity )
	{
		if ( entity is not Player { IsPlayer: true } player )
			return;

		for ( var i = 0; i < MaxEquip; i++ )
		{
			if ( string.IsNullOrEmpty( _weaponNames[i] ) )
				break;

			for ( var j = 0; j < _weaponCount[i]; j++ )
				player.GiveNamedItem( _weaponNames[i] );
		}
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		EquipPlayer( activator );
	}
}

[LinkEntityToClass( "game_player_team" )]
public class GamePlayerTeam : RulePointEntity
{
	private const int SpawnFlagFireOnce = 0x0001;
	private const int SpawnFlagKill = 0x0002;
	private const int SpawnFlagGib = 0x0004;

	private static string TargetTeamName( string targetName )
	{
		BaseEntity team = null;

		while ( (team = Util.FindEntityByTargetname( team, targetName )) is not null )
		{
			if ( team.ClassName == "game_team_master" )
				return team.TeamId;
		}

		return null;
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( !CanFireForActivator( activator ) )
			return;

		if ( activator.IsPlayer && TargetTeamName( Target ) is { } targetTeam )
			HalfLifeDll.GameRules.ChangePlayerTeam( (Player)activator, targetTeam, (SpawnFlags & SpawnFlagKill) != 0, (SpawnFlags & SpawnFlagGib) != 0 );

		if ( (SpawnFlags & SpawnFlagFireOnce) != 0 )
			Remove();
	}
}
