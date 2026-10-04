using System;
using GoldSrc;

namespace HalfLife;

public sealed class HalfLifeTeamplay : HalfLifeMultiplay
{
	private const int MaxTeamNameLength = 16;
	private const int MaxTeams = 32;
	private const int TeamListLength = MaxTeams * MaxTeamNameLength;

	private static string[] _teamNames = new string[MaxTeams];
	private static int[] _teamScores = new int[MaxTeams];
	private static int _teamCount;
	private static int _lastFrags;
	private static int _lastTime;

	private bool _disableDeathMessages;
	private bool _disableDeathPenalty;
	private bool _teamLimit;
	private string _teamList;

	public HalfLifeTeamplay( Server server ) : base( server )
	{
		_disableDeathMessages = false;
		_disableDeathPenalty = false;

		Array.Fill( _teamNames, "" );
		Array.Clear( _teamScores );
		_teamCount = 0;

		_teamList = Truncate( Cvar.GetString( "mp_teamlist" ), TeamListLength );

		if ( server.GetEdict( 0 ) is World { MapTeams: { Length: > 0 } mapTeams } && Cvar.GetFloat( "mp_teamoverride" ) != 0f )
			_teamList = Truncate( mapTeams, TeamListLength );

		_teamLimit = _teamList.Length != 0;

		RecountTeams();
	}

	private static string Truncate( string value, int length ) => value.Length > length ? value[..length] : value;

	public override void Think()
	{
		var fragsRemaining = 0;

		HalfLifeDll.VoiceGameMgr.Update( Server, Server.Globals.FrameTime );

		if ( HalfLifeDll.GameOver )
		{
			base.Think();
			return;
		}

		var timeLimit = Cvar.GetFloat( "mp_timelimit" ) * 60;
		var timeRemaining = (int)(timeLimit != 0f ? timeLimit - Server.Globals.Time : 0);

		if ( timeLimit != 0 && Server.Globals.Time >= timeLimit )
		{
			GoToIntermission();
			return;
		}

		var fragLimit = Cvar.GetFloat( "mp_fraglimit" );

		if ( fragLimit != 0f )
		{
			var bestFrags = 9999;

			for ( var i = 0; i < _teamCount; i++ )
			{
				if ( _teamScores[i] >= fragLimit )
				{
					GoToIntermission();
					return;
				}

				var remain = (int)(fragLimit - _teamScores[i]);

				if ( remain < bestFrags )
					bestFrags = remain;
			}

			fragsRemaining = bestFrags;
		}

		if ( fragsRemaining != _lastFrags )
			Cvar.Set( "mp_fragsleft", fragsRemaining.ToString() );

		if ( Cvar.GetFloat( "mp_timeleft" ) != _lastTime )
			Cvar.Set( "mp_timeleft", timeRemaining.ToString() );

		_lastFrags = fragsRemaining;
		_lastTime = timeRemaining;
	}

	public override bool ClientCommand( Player player, string command )
	{
		return HalfLifeDll.VoiceGameMgr.ClientCommand( player, command ) || command == "menuselect";
	}

	public override void UpdateGameMode( Player player )
	{
		Server.MessageBegin( MessageDest.One, UserMessages.GameMode, default, player );
		Server.WriteByte( 1 );
		Server.MessageEnd();
	}

	public override string SetDefaultPlayerTeam( Player player )
	{
		player.TeamName = Truncate( Info.ValueForKey( Server.GetInfoKeyBuffer( player ), "model" ), MaxTeamNameLength );

		RecountTeams();

		if ( player.TeamName.Length == 0 || !IsValidTeam( player.TeamName ) || Cvar.GetFloat( "mp_defaultteam" ) != 0f )
			player.TeamName = Truncate( Cvar.GetFloat( "mp_defaultteam" ) != 0f ? _teamNames[0] : TeamWithFewestPlayers(), MaxTeamNameLength );

		return player.TeamName;
	}

	public override void InitHud( Player player )
	{
		SetDefaultPlayerTeam( player );

		base.InitHud( player );

		Server.MessageBegin( MessageDest.One, UserMessages.TeamNames, default, player );
		Server.WriteByte( _teamCount );

		for ( var i = 0; i < _teamCount; i++ )
			Server.WriteString( _teamNames[i] );

		Server.MessageEnd();

		RecountTeams();

		var model = Info.ValueForKey( Server.GetInfoKeyBuffer( player ), "model" );
		var text = model == player.TeamName ? $"* you are on team '{player.TeamName}'\n" : $"* assigned to team {player.TeamName}\n";

		ChangePlayerTeam( player, player.TeamName, false, false );

		Util.SayText( text, player );

		RecountTeams();

		for ( var i = 1; i <= Server.MaxClients; i++ )
		{
			if ( Util.PlayerByIndex( i ) is not { } other || !IsValidTeam( other.TeamId ) )
				continue;

			Server.MessageBegin( MessageDest.One, UserMessages.TeamInfo, default, player );
			Server.WriteByte( other.Index );
			Server.WriteString( other.TeamId );
			Server.MessageEnd();
		}
	}

	public override void ChangePlayerTeam( Player player, string teamName, bool kill, bool gib )
	{
		var damageFlags = BaseEntity.DamageGeneric | (gib ? BaseEntity.DamageAlwaysGib : BaseEntity.DamageNeverGib);
		var clientIndex = player.Index;

		if ( kill )
		{
			_disableDeathMessages = true;
			_disableDeathPenalty = true;

			var world = (BaseEntity)Server.GetEdict( 0 );

			player.TakeDamage( world, world, 900f, damageFlags );

			_disableDeathMessages = false;
			_disableDeathPenalty = false;
		}

		player.TeamName = Truncate( teamName, MaxTeamNameLength );

		Server.SetClientKeyValue( clientIndex, ref Server.GetClient( player ).UserInfo, "model", player.TeamName );
		Server.SetClientKeyValue( clientIndex, ref Server.GetClient( player ).UserInfo, "team", player.TeamName );

		Server.MessageBegin( MessageDest.All, UserMessages.TeamInfo );
		Server.WriteByte( clientIndex );
		Server.WriteString( player.TeamName );
		Server.MessageEnd();

		Server.MessageBegin( MessageDest.All, UserMessages.ScoreInfo );
		Server.WriteByte( clientIndex );
		Server.WriteShort( (int)player.Frags );
		Server.WriteShort( player.Deaths );
		Server.WriteShort( 0 );
		Server.WriteShort( HalfLifeDll.GameRules.GetTeamIndex( player.TeamName ) + 1 );
		Server.MessageEnd();
	}

	public override void ClientUserInfoChanged( Player player, string info )
	{
		var model = Info.ValueForKey( info, "model" );

		if ( model.Equals( player.TeamName, StringComparison.OrdinalIgnoreCase ) )
			return;

		if ( Cvar.GetFloat( "mp_defaultteam" ) != 0f )
		{
			Server.SetClientKeyValue( player.Index, ref Server.GetClient( player ).UserInfo, "model", player.TeamName );
			Server.SetClientKeyValue( player.Index, ref Server.GetClient( player ).UserInfo, "team", player.TeamName );

			Util.SayText( "* Not allowed to change teams in this game!\n", player );
			return;
		}

		if ( !IsValidTeam( model ) )
		{
			Server.SetClientKeyValue( player.Index, ref Server.GetClient( player ).UserInfo, "model", player.TeamName );

			Util.SayText( $"* Can't change team to '{model}'\n", player );
			Util.SayText( $"* Server limits teams to '{_teamList}'\n", player );
			return;
		}

		Util.SayTextAll( $"* {player.NetName} has changed to team '{model}'\n", player );

		Server.LogPrintf( $"\"{player.NetName}<{Server.GetPlayerUserId( player )}><{Server.GetPlayerAuthId( player )}><{player.TeamName}>\" joined team \"{model}\"\n" );

		ChangePlayerTeam( player, model, true, true );

		RecountTeams( true );
	}

	public override void DeathNotice( Player victim, Edict killer, Edict inflictor )
	{
		if ( _disableDeathMessages )
			return;

		if ( victim is not null && killer is not null && (killer.EntityFlags & EntityFlags.Client) == EntityFlags.Client && killer is Player playerKiller
			&& playerKiller != victim && PlayerRelationship( victim, playerKiller ) == HalfLife.PlayerRelationship.Teammate )
		{
			Server.MessageBegin( MessageDest.All, UserMessages.DeathMsg );
			Server.WriteByte( killer.Index );
			Server.WriteByte( victim.Index );
			Server.WriteString( "teammate" );
			Server.MessageEnd();
			return;
		}

		base.DeathNotice( victim, killer, inflictor );
	}

	public override void PlayerKilled( Player victim, Edict killer, Edict inflictor )
	{
		if ( _disableDeathPenalty )
			return;

		base.PlayerKilled( victim, killer, inflictor );

		RecountTeams();
	}

	public override bool IsTeamplay() => true;

	public override bool PlayerCanTakeDamage( Player player, BaseEntity attacker )
	{
		if ( attacker is not null && PlayerRelationship( player, attacker ) == HalfLife.PlayerRelationship.Teammate
			&& Cvar.GetFloat( "mp_friendlyfire" ) == 0f && attacker != player )
		{
			return false;
		}

		return base.PlayerCanTakeDamage( player, attacker );
	}

	public override PlayerRelationship PlayerRelationship( BaseEntity player, BaseEntity target )
	{
		if ( player is null || target is null || !target.IsPlayer )
			return HalfLife.PlayerRelationship.NotTeammate;

		if ( GetTeamId( player ).Length != 0 && GetTeamId( target ).Length != 0 && GetTeamId( player ).Equals( GetTeamId( target ), StringComparison.OrdinalIgnoreCase ) )
			return HalfLife.PlayerRelationship.Teammate;

		return HalfLife.PlayerRelationship.NotTeammate;
	}

	public override bool ShouldAutoAim( Player player, Edict target )
	{
		if ( target is BaseEntity { IsPlayer: true } entity && PlayerRelationship( player, entity ) == HalfLife.PlayerRelationship.Teammate )
			return false;

		return base.ShouldAutoAim( player, target );
	}

	public override int PointsForKill( Player attacker, Player killed )
	{
		if ( killed is null )
			return 0;

		if ( attacker is null )
			return 1;

		return attacker != killed && PlayerRelationship( attacker, killed ) == HalfLife.PlayerRelationship.Teammate ? -1 : 1;
	}

	public override string GetTeamId( BaseEntity entity ) => entity is null ? "" : entity.TeamId;

	public override int GetTeamIndex( string teamName )
	{
		if ( string.IsNullOrEmpty( teamName ) )
			return -1;

		for ( var team = 0; team < _teamCount; team++ )
		{
			if ( _teamNames[team].Equals( teamName, StringComparison.OrdinalIgnoreCase ) )
				return team;
		}

		return -1;
	}

	public override string GetIndexedTeamName( int teamIndex )
	{
		return teamIndex < 0 || teamIndex >= _teamCount ? "" : _teamNames[teamIndex];
	}

	public override bool IsValidTeam( string teamName ) => !_teamLimit || GetTeamIndex( teamName ) != -1;

	private string TeamWithFewestPlayers()
	{
		var minPlayers = MaxTeams;
		var teamCount = new int[MaxTeams];
		string teamName = null;

		for ( var i = 1; i <= Server.MaxClients; i++ )
		{
			if ( Util.PlayerByIndex( i ) is not { } player )
				continue;

			var team = GetTeamIndex( player.TeamId );

			if ( team >= 0 )
				teamCount[team]++;
		}

		for ( var i = 0; i < _teamCount; i++ )
		{
			if ( teamCount[i] >= minPlayers )
				continue;

			minPlayers = teamCount[i];
			teamName = _teamNames[i];
		}

		return teamName;
	}

	private void RecountTeams( bool resendInfo = false )
	{
		_teamCount = 0;

		foreach ( var name in _teamList.Split( ';', StringSplitOptions.RemoveEmptyEntries ) )
		{
			if ( GetTeamIndex( name ) < 0 )
				_teamNames[_teamCount++] = name;
		}

		if ( _teamCount < 2 )
		{
			_teamCount = 0;
			_teamLimit = false;
		}

		Array.Clear( _teamScores );

		for ( var i = 1; i <= Server.MaxClients; i++ )
		{
			if ( Util.PlayerByIndex( i ) is not { } player )
				continue;

			var teamName = player.TeamId;
			var team = GetTeamIndex( teamName );

			if ( team < 0 && !_teamLimit )
			{
				team = _teamCount;
				_teamCount++;
				_teamScores[team] = 0;
				_teamNames[team] = Truncate( teamName, MaxTeamNameLength );
			}

			if ( team >= 0 )
				_teamScores[team] += (int)player.Frags;

			if ( !resendInfo || !IsValidTeam( player.TeamId ) )
				continue;

			Server.MessageBegin( MessageDest.All, UserMessages.TeamInfo );
			Server.WriteByte( player.Index );
			Server.WriteString( player.TeamId );
			Server.MessageEnd();
		}
	}
}
