using System;
using GoldSrc;
using GoldSrc.Vgui1;

namespace HalfLife;

using Color = GoldSrc.Vgui1.Color;

public sealed class VoiceLabel
{
	public Label Label;
	public Label Background;
	public ImagePanel Icon;
	public int ClientIndex;
}

public interface IVoiceStatusHelper
{
	void GetPlayerTextColor( int entIndex, int[] color );

	void UpdateCursorState();

	int GetAckIconHeight();

	bool CanShowSpeakerLabels();
}

public sealed class VoiceStatus( Hud hud ) : HudBase( hud )
{
	public const int VoiceMaxPlayers = 32;
	public const int VoiceMaxPlayersDWords = 1;
	public const int MaxVoiceSpeakers = 7;

	private const double ScoreboardBlinkFrequency = 0.3;

	[ConVar( "voice_modenable", ConVarFlags.Saved )]
	private static float VoiceModEnable { get; set; } = 1f;

	[ConVar( "voice_clientdebug" )]
	private static float VoiceClientDebug { get; set; }

	[ConCmd( "voice_showbanned" )]
	private static void ShowBanned()
	{
		if ( (ClientDll.Loaded as HalfLifeClientDll)?.Hud?.Voice is not { } voice )
			return;

		var count = 0;

		voice.Engine.ConsolePrint( "------- BANNED PLAYERS -------\n" );

		voice.BanMgr.ForEachBannedPlayer( id =>
		{
			var text = $"Ban {count++}: ";

			for ( var i = 0; i < VoiceBanMgr.PlayerIdLength; i++ )
				text += $"{(int)(sbyte)id[i],2:x}";

			voice.Engine.ConsolePrint( $"{text}\n".ToUpperInvariant() );
		} );

		voice.Engine.ConsolePrint( "------------------------------\n" );
	}

	public readonly VoiceBanMgr BanMgr = new();

	private readonly VoiceLabel[] _labels = new VoiceLabel[MaxVoiceSpeakers];
	private readonly Label[] _banButtons = new Label[VoiceMaxPlayers];
	private readonly byte[] _playerId = new byte[VoiceBanMgr.PlayerIdLength];

	private float _lastUpdateServerState;
	private int _serverModEnable = -1;
	private uint _voicePlayers;
	private uint _audiblePlayers;
	private uint _voiceEnabledPlayers;
	private uint _serverBannedPlayers;
	private IVoiceStatusHelper _helper;
	private double _blinkTimer;
	private BitmapTGA _scoreboardNeverSpoken;
	private BitmapTGA _scoreboardNotSpeaking;
	private BitmapTGA _scoreboardSpeaking;
	private BitmapTGA _scoreboardSpeaking2;
	public BitmapTGA ScoreboardSquelch;
	private BitmapTGA _scoreboardBanned;
	private bool _inSquelchMode;
	public int VoiceHeadModel;
	public float VoiceHeadModelHeight;
	private readonly ClientEntity[] _voiceHeadModels = new ClientEntity[VoiceMaxPlayers];
	private Image _speakerLabelIcon;
	private BitmapTGA _localBitmap;
	private BitmapTGA _ackBitmap;
	private ImagePanel _localLabel;
	private bool _talking;
	private bool _serverAcked;
	private bool _banMgrInitialized;
	private string _gameDir;

	public override void Init()
	{
	}

	public void CreateEntities()
	{
		if ( VoiceHeadModel == 0 )
			return;

		var localPlayer = Engine.GetLocalPlayer();
		var outModel = 0;

		for ( var i = 0; i < VoiceMaxPlayers; i++ )
		{
			if ( (_voicePlayers & (1u << i)) == 0 )
				continue;

			var client = Engine.GetEntityByIndex( i + 1 );

			if ( client is null || client.CurState.MessageNum < localPlayer.CurState.MessageNum )
				continue;

			if ( (client.CurState.Effects & (int)Effects.NoDraw) != 0 )
				continue;

			if ( client == localPlayer && !Hud.Dll.CamThirdPerson )
				continue;

			var entity = _voiceHeadModels[outModel] ??= Engine.CreateClientEntity( "voice head model" );

			outModel++;

			entity.Clear();

			entity.CurState.RenderMode = (int)RenderMode.TransAdd;
			entity.CurState.RenderAmt = 255;
			entity.CurState.RenderFx = (int)RenderFx.NoDissipation;
			entity.CurState.FrameRate = 1f;
			entity.CurState.Frame = 0f;
			entity.Model = Engine.GetSpritePointer( VoiceHeadModel );
			entity.CurState.Scale = 0.5f;
			entity.Origin = client.Origin + new Vector3( 0f, 0f, 45f );

			Engine.CreateVisibleEntity( ClientDll.EntityTypeNormal, entity );
		}
	}

	public void Init( IVoiceStatusHelper helper )
	{
		BanMgr.Init( Server.GameDirectory );
		_banMgrInitialized = true;

		_blinkTimer = 0;
		VoiceHeadModel = 0;

		for ( var i = 0; i < MaxVoiceSpeakers; i++ )
		{
			var label = _labels[i] = new VoiceLabel();

			label.Background = new Label( "" );

			label.Label = new Label( "" );
			label.Label.SetVisible( true );
			label.Label.SetFont( SchemeFont.Primary2 );
			label.Label.SetTextAlignment( Label.Alignment.East );
			label.Label.SetContentAlignment( Label.Alignment.East );
			label.Label.SetParent( label.Background );

			label.Icon = new ImagePanel( null );
			label.Icon.SetVisible( true );
			label.Icon.SetParent( label.Background );

			label.ClientIndex = -1;
		}

		_localLabel = new ImagePanel( null );

		_inSquelchMode = false;

		_helper = helper;

		Hud.AddHudElem( this );
		Flags = Active;

		Hud.Dll.HookUserMsg( "VoiceMask", MsgFuncVoiceMask );
		Hud.Dll.HookUserMsg( "ReqState", MsgFuncReqState );

		_gameDir = Server.GameDirectory;
	}

	public void Shutdown()
	{
		if ( _gameDir is not null && _banMgrInitialized )
			BanMgr.SaveState( _gameDir );
	}

	private bool MsgFuncVoiceMask( string name, byte[] data )
	{
		HandleVoiceMaskMsg( data );

		return true;
	}

	private bool MsgFuncReqState( string name, byte[] data )
	{
		HandleReqStateMsg();

		return true;
	}

	public override void VidInit()
	{
		FreeBitmaps();

		_localBitmap = VguiLoadTga.LoadTga( "gfx/vgui/icntlk_pl.tga" );
		_localBitmap?.SetColor( new Color( 255, 255, 255, 135 ) );

		_ackBitmap = VguiLoadTga.LoadTga( "gfx/vgui/icntlk_sv.tga" );
		_ackBitmap?.SetColor( new Color( 255, 255, 255, 135 ) );

		_localLabel.SetImage( _localBitmap );
		_localLabel.SetVisible( false );

		_speakerLabelIcon = VguiLoadTga.LoadTgaNoInvertAlpha( "gfx/vgui/speaker4.tga" );
		_speakerLabelIcon?.SetColor( new Color( 255, 255, 255, 1 ) );

		_scoreboardNeverSpoken = VguiLoadTga.LoadTgaNoInvertAlpha( "gfx/vgui/640_speaker1.tga" );
		_scoreboardNeverSpoken?.SetColor( new Color( 255, 255, 255, 1 ) );

		_scoreboardNotSpeaking = VguiLoadTga.LoadTgaNoInvertAlpha( "gfx/vgui/640_speaker2.tga" );
		_scoreboardNotSpeaking?.SetColor( new Color( 255, 255, 255, 1 ) );

		_scoreboardSpeaking = VguiLoadTga.LoadTgaNoInvertAlpha( "gfx/vgui/640_speaker3.tga" );
		_scoreboardSpeaking?.SetColor( new Color( 255, 255, 255, 1 ) );

		_scoreboardSpeaking2 = VguiLoadTga.LoadTgaNoInvertAlpha( "gfx/vgui/640_speaker4.tga" );
		_scoreboardSpeaking2?.SetColor( new Color( 255, 255, 255, 1 ) );

		ScoreboardSquelch = VguiLoadTga.LoadTga( "gfx/vgui/icntlk_squelch.tga" );
		ScoreboardSquelch?.SetColor( new Color( 255, 255, 255, 1 ) );

		_scoreboardBanned = VguiLoadTga.LoadTga( "gfx/vgui/640_voiceblocked.tga" );
		_scoreboardBanned?.SetColor( new Color( 255, 255, 255, 1 ) );

		VoiceHeadModelHeight = 45;

		if ( Server.LoadMountText( "scripts/voicemodel.txt" ) is { } file )
		{
			var position = 0;
			var token = Common.Parse( file, ref position ) ?? "";

			if ( token.Length != 0 && token[0] >= '0' && token[0] <= '9' )
				VoiceHeadModelHeight = Common.Atof( token );
		}

		VoiceHeadModel = Engine.SpriteLoad( "sprites/voiceicon.spr" );
	}

	public void Frame( double frameTime )
	{
		if ( Engine.ClientTime - _lastUpdateServerState > 1 )
			UpdateServerState( false );

		_blinkTimer += frameTime;

		if ( _helper.CanShowSpeakerLabels() )
		{
			for ( var i = 0; i < MaxVoiceSpeakers; i++ )
				_labels[i].Background.SetVisible( _labels[i].ClientIndex != -1 );
		}
		else
		{
			for ( var i = 0; i < MaxVoiceSpeakers; i++ )
				_labels[i].Background.SetVisible( false );
		}

		for ( var i = 0; i < VoiceMaxPlayers; i++ )
			UpdateBanButton( i );
	}

	public void UpdateSpeakerStatus( int entIndex, bool talking )
	{
		var loopback = false;

		if ( Hud.ViewPort is null )
			return;

		if ( VoiceClientDebug != 0f )
			Engine.ConsolePrint( $"CVoiceStatus::UpdateSpeakerStatus: ent {entIndex} talking = {(talking ? 1 : 0)}\n" );

		var localPlayerIndex = Engine.GetLocalPlayer().Index;

		if ( entIndex == -1 )
		{
			_talking = talking;

			if ( talking )
				Client.ClientCmd( "voice_modenable 1" );

			entIndex = localPlayerIndex;

			loopback = Cvar.Exists( "voice_loopback" ) && Cvar.GetFloat( "voice_loopback" ) != 0f;
		}
		else if ( entIndex == -2 )
		{
			_serverAcked = talking;
		}

		if ( entIndex >= 0 && entIndex <= VoiceMaxPlayers )
		{
			var client = entIndex - 1;

			if ( client < 0 )
				return;

			var label = FindVoiceLabel( client );

			if ( talking )
			{
				_voicePlayers |= 1u << client;
				_voiceEnabledPlayers |= 1u << client;

				if ( label is null )
				{
					if ( entIndex != localPlayerIndex || loopback )
					{
						label = GetFreeVoiceLabel();

						if ( label is not null )
						{
							var paddedName = $"{Engine.GetPlayerInfo( entIndex - 1 )?.Name ?? ""}   ";

							var color = new int[3];

							_helper.GetPlayerTextColor( entIndex, color );

							label.Background.SetBgColor( color[0], color[1], color[2], 135 );
							label.Background.SetParent( Hud.ViewPort );
							label.Background.SetVisible( _helper.CanShowSpeakerLabels() );

							label.Label.SetFgColor( 255, 255, 255, 0 );
							label.Label.SetBgColor( 0, 0, 0, 255 );
							label.Label.SetText( paddedName );

							label.ClientIndex = client;
						}
					}
				}
			}
			else
			{
				_voicePlayers &= ~(1u << client);

				if ( label is not null )
				{
					label.Background.SetVisible( false );
					label.ClientIndex = -1;
				}
			}
		}

		RepositionLabels();
	}

	public void UpdateServerState( bool force )
	{
		if ( Engine.LevelName.Length == 0 )
		{
			if ( VoiceClientDebug != 0f )
				Engine.ConsolePrint( "CVoiceStatus::UpdateServerState: pLevelName[0]==0\n" );

			return;
		}

		var cvarModEnable = VoiceModEnable != 0f ? 1 : 0;

		if ( force || _serverModEnable != cvarModEnable )
		{
			_serverModEnable = cvarModEnable;

			var command = $"VModEnable {_serverModEnable}";

			Engine.ServerCmd( command );

			if ( VoiceClientDebug != 0f )
				Engine.ConsolePrint( $"CVoiceStatus::UpdateServerState: Sending '{command}'\n" );
		}

		var text = "vban";
		var change = false;

		for ( var dw = 0; dw < VoiceMaxPlayersDWords; dw++ )
		{
			uint serverBanMask = 0;
			uint banMask = 0;

			for ( var i = 0; i < 32; i++ )
			{
				if ( !Engine.GetPlayerUniqueId( i + 1, _playerId ) )
					continue;

				if ( BanMgr.GetPlayerBan( _playerId ) )
					banMask |= 1u << i;

				if ( (_serverBannedPlayers & (1u << i)) != 0 )
					serverBanMask |= 1u << i;
			}

			if ( serverBanMask != banMask )
				change = true;

			text += $" {banMask:x}";
		}

		if ( change || force )
		{
			if ( VoiceClientDebug != 0f )
				Engine.ConsolePrint( $"CVoiceStatus::UpdateServerState: Sending '{text}'\n" );

			Engine.ServerCmdUnreliable( text );
		}
		else if ( VoiceClientDebug != 0f )
		{
			Engine.ConsolePrint( "CVoiceStatus::UpdateServerState: no change\n" );
		}

		_lastUpdateServerState = (float)Engine.ClientTime;
	}

	public void UpdateSpeakerImage( Label label, int player )
	{
		_banButtons[player - 1] = label;

		UpdateBanButton( player - 1 );
	}

	public void UpdateBanButton( int client )
	{
		var panel = _banButtons[client];

		if ( panel is null )
			return;

		if ( !Engine.GetPlayerUniqueId( client + 1, _playerId ) )
			return;

		var blink = (_blinkTimer % (ScoreboardBlinkFrequency * 2)) < ScoreboardBlinkFrequency;
		var talking = (_voicePlayers & (1u << client)) != 0;
		var banned = BanMgr.GetPlayerBan( _playerId );
		var neverSpoken = (_voiceEnabledPlayers & (1u << client)) == 0;

		if ( banned )
		{
			panel.SetImage( _scoreboardBanned );
		}
		else if ( talking )
		{
			panel.SetImage( blink ? _scoreboardSpeaking2 : _scoreboardSpeaking );
			panel.SetFgColor( 255, 170, 0, 1 );
		}
		else if ( neverSpoken )
		{
			panel.SetImage( _scoreboardNeverSpoken );
			panel.SetFgColor( 100, 100, 100, 1 );
		}
		else
		{
			panel.SetImage( _scoreboardNotSpeaking );
		}
	}

	private void HandleVoiceMaskMsg( byte[] data )
	{
		var message = new MessageBuffer( data );

		for ( var dw = 0; dw < VoiceMaxPlayersDWords; dw++ )
		{
			_audiblePlayers = (uint)message.ReadLong();
			_serverBannedPlayers = (uint)message.ReadLong();

			if ( VoiceClientDebug != 0f )
			{
				Engine.ConsolePrint( "CVoiceStatus::HandleVoiceMaskMsg\n" );
				Engine.ConsolePrint( $"    - m_AudiblePlayers[{dw}] = {_audiblePlayers}\n" );
				Engine.ConsolePrint( $"    - m_ServerBannedPlayers[{dw}] = {_serverBannedPlayers}\n" );
			}
		}

		_serverModEnable = message.ReadByte();
	}

	private void HandleReqStateMsg()
	{
		if ( VoiceClientDebug != 0f )
			Engine.ConsolePrint( "CVoiceStatus::HandleReqStateMsg\n" );

		UpdateServerState( true );
	}

	public void StartSquelchMode()
	{
		if ( _inSquelchMode )
			return;

		_inSquelchMode = true;
		_helper.UpdateCursorState();
	}

	public void StopSquelchMode()
	{
		_inSquelchMode = false;
		_helper.UpdateCursorState();
	}

	public bool IsInSquelchMode() => _inSquelchMode;

	public VoiceLabel FindVoiceLabel( int clientIndex )
	{
		for ( var i = 0; i < MaxVoiceSpeakers; i++ )
		{
			if ( _labels[i].ClientIndex == clientIndex )
				return _labels[i];
		}

		return null;
	}

	public VoiceLabel GetFreeVoiceLabel() => FindVoiceLabel( -1 );

	public void RepositionLabels()
	{
		var y = Hud.ScreenHeight / 2;

		var iconWide = 8;
		var iconTall = 8;

		_speakerLabelIcon?.GetSize( out iconWide, out iconTall );

		for ( var i = 0; i < MaxVoiceSpeakers; i++ )
		{
			var label = _labels[i];

			if ( label.ClientIndex == -1 )
			{
				label.Background.SetVisible( false );
				continue;
			}

			label.Label.GetContentSize( out var textWide, out var textTall );

			if ( textWide > Hud.ScreenWidth * 2 / 3 )
				textWide = Hud.ScreenWidth * 2 / 3;

			const int border = 2;

			var bgWide = textWide + iconWide + (border * 3);
			var bgTall = Math.Max( textTall, iconTall ) + (border * 2);

			label.Background.SetBounds( Hud.ScreenWidth - bgWide - 8, y, bgWide, bgTall );

			label.Label.SetBounds( border, (bgTall - textTall) / 2, textWide, textTall );

			var iconLeft = border + textWide + border;
			var iconTop = (bgTall - iconTall) / 2;

			label.Icon.SetImage( _speakerLabelIcon );
			label.Icon.SetBounds( iconLeft, iconTop, iconWide, iconTall );

			y += bgTall + 2;
		}

		if ( _localBitmap is not null && _ackBitmap is not null && (_talking || _serverAcked) )
		{
			_localLabel.SetParent( Hud.ViewPort );
			_localLabel.SetVisible( true );

			if ( _serverAcked && VoiceClientDebug != 0f )
				_localLabel.SetImage( _ackBitmap );
			else
				_localLabel.SetImage( _localBitmap );

			_localBitmap.GetSize( out var sizeX, out var sizeY );

			_localLabel.SetPos( Hud.ScreenWidth - sizeX - 10, _helper.GetAckIconHeight() - sizeY );
		}
		else
		{
			_localLabel.SetVisible( false );
		}
	}

	private void FreeBitmaps()
	{
		_localBitmap = null;
		_ackBitmap = null;
		_speakerLabelIcon = null;
		_scoreboardNeverSpoken = null;
		_scoreboardNotSpeaking = null;
		_scoreboardSpeaking = null;
		_scoreboardSpeaking2 = null;
		ScoreboardSquelch = null;
		_scoreboardBanned = null;

		for ( var i = 0; i < VoiceMaxPlayers; i++ )
			_banButtons[i]?.SetImage( null );

		_localLabel?.SetImage( null );
	}

	public bool IsPlayerBlocked( int player )
	{
		if ( !Engine.GetPlayerUniqueId( player, _playerId ) )
			return false;

		return BanMgr.GetPlayerBan( _playerId );
	}

	public bool IsPlayerAudible( int player ) => (_audiblePlayers & (1u << (player - 1))) != 0;

	public void SetPlayerBlockedState( int player, bool blocked )
	{
		if ( VoiceClientDebug != 0f )
			Engine.ConsolePrint( "CVoiceStatus::SetPlayerBlockedState part 1\n" );

		if ( !Engine.GetPlayerUniqueId( player, _playerId ) )
			return;

		if ( VoiceClientDebug != 0f )
			Engine.ConsolePrint( "CVoiceStatus::SetPlayerBlockedState part 2\n" );

		if ( VoiceClientDebug != 0f )
			Engine.ConsolePrint( $"CVoiceStatus::SetPlayerBlockedState: setting player {player} ban to {(BanMgr.GetPlayerBan( _playerId ) ? 0 : 1)}\n" );

		BanMgr.SetPlayerBan( _playerId, blocked );

		UpdateServerState( false );
	}
}
