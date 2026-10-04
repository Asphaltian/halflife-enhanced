using System;
using GoldSrc;

namespace HalfLife;

public sealed class HudDeathNotice( Hud hud ) : HudBase( hud )
{
	private const int MaxDeathNotices = 4;
	private const int DeathNoticeTop = 32;
	private const int MaxKilledWithLength = 32;
	private const int NonPlayerVictim = 255;

	public static readonly float[] ColorBlue = [0.6f, 0.8f, 1.0f];
	public static readonly float[] ColorRed = [1.0f, 0.25f, 0.25f];
	public static readonly float[] ColorGreen = [0.6f, 1.0f, 0.6f];
	public static readonly float[] ColorYellow = [1.0f, 0.7f, 0.0f];
	public static readonly float[] ColorGrey = [0.8f, 0.8f, 0.8f];

	private struct DeathNoticeItem
	{
		public string Killer;
		public string Victim;
		public int Id;
		public bool Suicide;
		public bool TeamKill;
		public bool NonPlayerKill;
		public float DisplayTime;
		public float[] KillerColor;
		public float[] VictimColor;
	}

	private readonly DeathNoticeItem[] _list = new DeathNoticeItem[MaxDeathNotices + 1];

	private int _displayTime = 6;
	private int _skull;

	[ConVar( "hud_deathnotice_time" )]
	private static float DeathNoticeTime { get; set; } = 6f;

	public override void Init()
	{
		Hud.AddHudElem( this );

		Hud.Dll.HookUserMsg( "DeathMsg", MsgFuncDeathMsg );
	}

	public override void InitHudData() => Array.Clear( _list );

	public override void VidInit() => _skull = Hud.GetSpriteIndex( "d_skull" );

	private static int Width( SpriteRect rect ) => rect.Right - rect.Left;

	public override void Draw( float time )
	{
		var sprite = Hud.GetSpriteRect( _skull );
		var gap = Math.Max( sprite.Bottom - sprite.Top, Engine.CharacterHeight );

		for ( var i = 0; i < MaxDeathNotices; i++ )
		{
			if ( _list[i].Id == 0 )
				break;

			if ( _list[i].DisplayTime < time )
			{
				Array.Copy( _list, i + 1, _list, i, MaxDeathNotices - i );
				i--;
				continue;
			}

			_list[i].DisplayTime = Math.Min( _list[i].DisplayTime, Hud.Time + _displayTime );

			if ( Hud.ViewPort is not { } viewPort || !viewPort.AllowedToPrintText() )
				continue;

			var item = _list[i];
			var y = DeathNoticeTop + 2 + (gap * i);
			var textY = y + 4;
			var id = item.Id == -1 ? _skull : item.Id;

			Engine.DrawConsoleStringLen( item.Victim ?? "", out var victimWidth, out _ );

			var x = Hud.ScreenWidth - victimWidth - Width( Hud.GetSpriteRect( id ) ) - 4;

			if ( !item.Suicide )
			{
				Engine.DrawConsoleStringLen( item.Killer ?? "", out var killerWidth, out _ );

				x -= 5 + killerWidth;

				if ( item.KillerColor is { } killerColor )
					Engine.DrawSetTextColor( killerColor[0], killerColor[1], killerColor[2] );

				x = 5 + Engine.DrawConsoleString( x, textY, item.Killer ?? "" );
			}

			var r = 255;
			var g = 80;
			var b = 0;

			if ( item.TeamKill )
			{
				r = 10;
				g = 240;
				b = 10;
			}

			Engine.SpriteSet( Hud.GetSprite( id ), r, g, b );
			Engine.SpriteDrawAdditive( 0, x, y, Hud.GetSpriteRect( id ) );

			x += Width( Hud.GetSpriteRect( id ) );

			if ( !item.NonPlayerKill )
			{
				if ( item.VictimColor is { } victimColor )
					Engine.DrawSetTextColor( victimColor[0], victimColor[1], victimColor[2] );

				Engine.DrawConsoleString( x, textY, item.Victim ?? "" );
			}
		}
	}

	private static string CopyName( string name )
	{
		return name.Length >= Hud.MaxPlayerNameLength ? name[..(Hud.MaxPlayerNameLength - 1)] : name;
	}

	private bool MsgFuncDeathMsg( string name, byte[] data )
	{
		Flags |= Active;

		var message = new MessageBuffer( data );
		var killer = message.ReadByte();
		var victim = message.ReadByte();
		var weapon = message.ReadString();
		var killedWith = $"d_{(weapon.Length > MaxKilledWithLength ? weapon[..MaxKilledWithLength] : weapon)}";

		Hud.ViewPort?.DeathMsg( killer, victim );
		Hud.Spectator.DeathMessage( victim );

		int i;

		for ( i = 0; i < MaxDeathNotices; i++ )
		{
			if ( _list[i].Id == 0 )
				break;
		}

		if ( i == MaxDeathNotices )
		{
			Array.Copy( _list, 1, _list, 0, MaxDeathNotices );
			i = MaxDeathNotices - 1;
		}

		Hud.ViewPort?.GetAllPlayersInfo();

		ref var item = ref _list[i];

		if ( Engine.GetPlayerName( killer ) is { } killerName )
		{
			item.KillerColor = Hud.GetClientColor( killer );
			item.Killer = CopyName( killerName );
		}
		else
		{
			item.Killer = "";
		}

		if ( victim != NonPlayerVictim && Engine.GetPlayerName( victim ) is { } victimName )
		{
			item.VictimColor = Hud.GetClientColor( victim );
			item.Victim = CopyName( victimName );
		}
		else
		{
			item.Victim = "";
		}

		if ( victim == NonPlayerVictim )
		{
			item.NonPlayerKill = true;
			item.Victim = killedWith[2..];
		}
		else
		{
			if ( killer == victim || killer == 0 )
				item.Suicide = true;

			if ( killedWith == "d_teammate" )
				item.TeamKill = true;
		}

		item.Id = Hud.GetSpriteIndex( killedWith );

		_displayTime = (int)DeathNoticeTime;
		item.DisplayTime = Hud.Time + _displayTime;

		if ( item.NonPlayerKill )
		{
			Engine.ConsolePrint( $"{item.Killer} killed a {item.Victim}\n" );
			return true;
		}

		string text;

		if ( item.Suicide )
			text = item.Victim + (killedWith == "d_world" ? " died" : " killed self");
		else if ( item.TeamKill )
			text = $"{item.Killer} killed his teammate {item.Victim}";
		else
			text = $"{item.Killer} killed {item.Victim}";

		if ( killedWith != "d_world" && !item.TeamKill )
		{
			if ( killedWith[2..] == "egon" )
				killedWith = "d_gluon gun";

			if ( killedWith[2..] == "gauss" )
				killedWith = "d_tau cannon";

			text += $" with {killedWith[2..]}";
		}

		Engine.ConsolePrint( $"{text}\n" );

		return true;
	}
}
