using System;
using GoldSrc;
using GoldSrc.Vgui1;

namespace HalfLife;

using Color = GoldSrc.Vgui1.Color;

public class TextImage2 : Image
{
	private readonly TextImage[] _image = [new TextImage( "" ), new TextImage( "" )];

	public TextImage GetImage( int image ) => _image[image];

	public override void GetSize( out int wide, out int tall )
	{
		_image[0].GetTextSize( out var w1, out var t1 );
		_image[1].GetTextSize( out var w2, out var t2 );

		wide = w1 + w2;
		tall = Math.Max( t1, t2 );

		SetSize( wide, tall );
	}

	public override void DoPaint( Panel panel )
	{
		_image[0].DoPaint( panel );
		_image[1].DoPaint( panel );
	}

	public override void SetPos( int x, int y )
	{
		_image[0].SetPos( x, y );
		_image[0].GetSize( out _, out var stall );
		_image[1].GetSize( out var wide, out var tall );
		_image[1].SetPos( x + wide, (int)(y + (stall * 0.9) - tall) );
	}

	public override void SetColor( Color color ) => _image[0].SetColor( color );

	public void SetColor2( Color color ) => _image[1].SetColor( color );
}

public class LabelHeader : Label
{
	private readonly Hud _hud;
	private readonly TextImage2 _dualImage;
	private int _row;
	private int _gap;
	private readonly int[] _offset = new int[2];
	private bool _useFgColorAsImageColor;

	public LabelHeader( Hud hud ) : base( "" )
	{
		_hud = hud;
		_dualImage = new TextImage2();
		_dualImage.SetColor2( new Color( 255, 170, 0, 0 ) );
		_row = -2;
		_useFgColorAsImageColor = true;
		_offset[0] = 0;
		_offset[1] = 0;
	}

	public void SetRow( int row ) => _row = row;

	public void SetFgColorAsImageColor( bool state ) => _useFgColorAsImageColor = state;

	public override void SetText( int textBufferLen, string text ) => LabelHeaderSetText( text );

	private void LabelHeaderSetText( string text )
	{
		_dualImage.GetImage( 0 ).SetText( text );

		var font = _dualImage.GetImage( 0 ).GetFont();

		_gap = 0;

		foreach ( var ch in text )
		{
			font.GetCharAbcWide( (sbyte)ch, out var a, out var b, out var c );

			_gap += a + b + c;
		}

		_gap = (int)(_gap + VguiUtil.XRes( 5 ));
	}

	public new void SetText( string text )
	{
		var length = text.Length;

		while ( length != 0 && IsSpace( text[length - 1] ) )
			length--;

		LabelHeaderSetText( text[..length] );
	}

	private static bool IsSpace( char ch ) => ch is ' ' or '\t' or '\n' or '\v' or '\f' or '\r';

	public void SetText2( string text ) => _dualImage.GetImage( 1 ).SetText( text );

	public override void GetTextSize( out int wide, out int tall ) => _dualImage.GetSize( out wide, out tall );

	public override void SetFgColor( int r, int g, int b, int a )
	{
		base.SetFgColor( r, g, b, a );

		var color = new Color( r, g, b, a );

		_dualImage.SetColor( color );
		_dualImage.SetColor2( color );

		Repaint();
	}

	public override void SetFgColor( SchemeColor sc )
	{
		base.SetFgColor( sc );

		GetFgColor( out var r, out var g, out var b, out var a );

		SetFgColor( r, g, b, a );
	}

	public override void SetFont( Font font ) => _dualImage.GetImage( 0 ).SetFont( font );

	public void SetFont2( Font font ) => _dualImage.GetImage( 1 ).SetFont( font );

	public void SetTextOffset( int x, int y )
	{
		_offset[0] = x;
		_offset[1] = y;
	}

	public override void Paint()
	{
		GetFgColor( out Color oldFg );

		if ( _hud.ViewPort.GetScoreBoard().HighlightRow == _row )
			SetFgColor( 255, 255, 255, 0 );

		GetTextSize( out var iwide, out var itall );
		CalcAlignment( iwide, itall, out var x, out var y );

		_dualImage.SetPos( x, y );
		_dualImage.GetImage( 1 ).GetPos( out _, out var y1 );
		_dualImage.GetImage( 1 ).SetPos( _gap, y1 );
		_dualImage.DoPaint( this );

		if ( _image is not null )
		{
			GetFgColor( out Color imageColor );

			if ( _useFgColorAsImageColor )
				_image.SetColor( imageColor );

			_image.GetSize( out iwide, out itall );
			CalcAlignment( iwide, itall, out x, out y );

			_image.SetPos( x, y );
			_image.DoPaint( this );
		}

		SetFgColor( oldFg[0], oldFg[1], oldFg[2], oldFg[3] );
	}

	public override void PaintBackground()
	{
		GetBgColor( out Color oldBg );

		if ( _hud.ViewPort.GetScoreBoard().HighlightRow == _row )
			SetBgColor( 134, 91, 19, 0 );

		base.PaintBackground();

		SetBgColor( oldBg );
	}

	public void CalcAlignment( int iwide, int itall, out int x, out int y )
	{
		GetSize( out var wide, out var tall );

		x = 0;
		y = 0;

		switch ( _contentAlignment )
		{
			case Alignment.Northwest:
			case Alignment.West:
			case Alignment.Southwest:
				x = 0;
				break;

			case Alignment.North:
			case Alignment.Center:
			case Alignment.South:
				x = (wide - iwide) / 2;
				break;

			case Alignment.Northeast:
			case Alignment.East:
			case Alignment.Southeast:
				x = wide - iwide;
				break;
		}

		switch ( _contentAlignment )
		{
			case Alignment.Northwest:
			case Alignment.North:
			case Alignment.Northeast:
				y = 0;
				break;

			case Alignment.West:
			case Alignment.Center:
			case Alignment.East:
				y = (tall - itall) / 2;
				break;

			case Alignment.Southwest:
			case Alignment.South:
			case Alignment.Southeast:
				y = tall - itall;
				break;
		}

		if ( x < 0 )
			x = 0;

		x += _offset[0];
		y += _offset[1];
	}
}

public sealed class ScorePanel : Panel, InputSignal
{
	private sealed class HitTestPanel : Panel
	{
		public override void InternalMousePressed( MouseCode code )
		{
			for ( var i = 0; i < _inputSignalDar.Count; i++ )
				_inputSignalDar[i].MousePressed( code, this );
		}
	}

	public const int MaxScoreboardTeams = 5;

	public const int ColumnTracker = 0;
	public const int ColumnName = 1;
	public const int ColumnClass = 2;
	public const int ColumnKills = 3;
	public const int ColumnDeaths = 4;
	public const int ColumnLatency = 5;
	public const int ColumnVoice = 6;
	public const int ColumnBlank = 7;
	public const int NumColumns = 8;
	public const int NumRows = Hud.MaxPlayers + (MaxScoreboardTeams * 2);

	private const int TeamNo = 0;
	private const int TeamYes = 1;
	private const int TeamSpectators = 2;
	private const int TeamBlank = 3;

	[SkipHotload]
	private static readonly (string Title, int Width, Label.Alignment Alignment)[] ColumnInfo =
	[
		(null, 24, Label.Alignment.East),
		(null, 140, Label.Alignment.East),
		(null, 56, Label.Alignment.East),
		("#SCORE", 40, Label.Alignment.East),
		("#DEATHS", 46, Label.Alignment.East),
		("#LATENCY", 46, Label.Alignment.East),
		("#VOICE", 40, Label.Alignment.East),
		(null, 2, Label.Alignment.East)
	];

	private readonly Hud _hud;
	private readonly Label _titleLabel = new();
	private readonly Grid _headerGrid = new();
	private readonly LabelHeader[] _headerLabels = new LabelHeader[NumColumns];
	private LabelHeader _currentHighlightLabel;
	private readonly ListBox _playerList = new();
	private readonly Grid[] _playerGrids = new Grid[NumRows];
	private readonly LabelHeader[,] _playerEntries = new LabelHeader[NumColumns, NumRows];
	private readonly HitTestPanel _hitTestPanel = new();
	private readonly CommandButton _closeButton;
	private readonly BitmapTGA _trackerIcon;

	public int HighlightRow;
	public int NumTeams;
	public int PlayerNum;
	public int ShowscoresHeld;
	public int Rows;
	public readonly int[] SortedRows = new int[NumRows];
	public readonly int[] IsATeam = new int[NumRows];
	public readonly bool[] HasBeenSorted = new bool[Hud.MaxPlayers];
	public int LastKilledBy;
	public int LastKillTime;

	public ScorePanel( Hud hud, int x, int y, int wide, int tall ) : base( x, y, wide, tall )
	{
		_hud = hud;

		for ( var i = 0; i < NumColumns; i++ )
			_headerLabels[i] = new LabelHeader( hud );

		for ( var row = 0; row < NumRows; row++ )
			_playerGrids[row] = new Grid();

		for ( var col = 0; col < NumColumns; col++ )
		{
			for ( var row = 0; row < NumRows; row++ )
				_playerEntries[col, row] = new LabelHeader( hud );
		}

		var schemes = hud.ViewPort.GetSchemeManager();
		var titleScheme = schemes.GetSchemeHandle( "Scoreboard Title Text" );
		var smallScheme = schemes.GetSchemeHandle( "Scoreboard Small Text" );
		var titleFont = schemes.GetFont( titleScheme );
		var smallFont = schemes.GetFont( smallScheme );

		SetBgColor( 0, 0, 0, 96 );

		_currentHighlightLabel = null;
		HighlightRow = -1;

		_trackerIcon = VguiLoadTga.LoadTgaNoInvertAlpha( "gfx/vgui/640_scoreboardtracker.tga" );

		_titleLabel.SetFont( titleFont );
		_titleLabel.SetText( "" );
		_titleLabel.SetBgColor( 0, 0, 0, 255 );
		_titleLabel.SetFgColor( SchemeColor.Primary1 );
		_titleLabel.SetContentAlignment( Label.Alignment.West );

		SetBorder( new LineBorder( new Color( 60, 60, 60, 128 ) ) );
		SetPaintBorderEnabled( true );

		var xpos = ColumnInfo[0].Width + 3;

		if ( VguiUtil.ScreenWidth >= 640 )
			xpos = (int)VguiUtil.XRes( xpos );

		_titleLabel.SetBounds( xpos, 4, wide, (int)VguiUtil.YRes( 22 ) );
		_titleLabel.SetContentFitted( false );
		_titleLabel.SetParent( this );

		_headerGrid.SetDimensions( NumColumns, 1 );
		_headerGrid.SetSpacing( 0, 0 );

		for ( var i = 0; i < NumColumns; i++ )
		{
			var title = ColumnInfo[i].Title;

			if ( title is not null && title[0] == '#' )
				_headerLabels[i].SetText( hud.TextMessage.BufferedLocaliseTextString( title ) );
			else if ( title is not null )
				_headerLabels[i].SetText( title );

			var xwide = ColumnInfo[i].Width;

			if ( VguiUtil.ScreenWidth >= 640 )
			{
				xwide = (int)VguiUtil.XRes( xwide );
			}
			else if ( VguiUtil.ScreenWidth == 400 )
			{
				if ( i == 1 )
					xwide -= 28;
				else if ( i == 0 )
					xwide -= 8;
			}

			_headerGrid.SetColumnWidth( i, xwide );
			_headerGrid.SetEntry( i, 0, _headerLabels[i] );

			_headerLabels[i].SetBgColor( 0, 0, 0, 255 );
			_headerLabels[i].SetFgColor( SchemeColor.Primary1 );
			_headerLabels[i].SetFont( smallFont );
			_headerLabels[i].SetContentAlignment( ColumnInfo[i].Alignment );

			var yres = 12;

			if ( VguiUtil.ScreenHeight >= 480 )
				yres = (int)VguiUtil.YRes( yres );

			_headerLabels[i].SetSize( 50, yres );
		}

		var border = VguiUtil.XRes( 4 );

		_headerGrid.GetEntryBox( NumColumns - 2, 0, out var ex, out _, out var ew, out _ );
		_headerGrid.SetColumnWidth( NumColumns - 1, (int)(wide - border - (ex + ew)) );

		_headerGrid.AutoSetRowHeights();
		_headerGrid.SetBounds( (int)border, (int)VguiUtil.YRes( 22 ), (int)(wide - (border * 2)), _headerGrid.GetRowHeight( 0 ) );
		_headerGrid.SetParent( this );
		_headerGrid.SetBgColor( 0, 0, 0, 255 );

		_headerGrid.GetBounds( out var headerX, out var headerY, out var headerWidth, out var headerHeight );

		_playerList.SetBounds( headerX, headerY + headerHeight, headerWidth, tall - headerY - headerHeight - 6 );
		_playerList.SetBgColor( 0, 0, 0, 255 );
		_playerList.SetParent( this );

		for ( var row = 0; row < NumRows; row++ )
		{
			var gridRow = _playerGrids[row];

			gridRow.SetDimensions( NumColumns, 1 );

			for ( var col = 0; col < NumColumns; col++ )
			{
				_playerEntries[col, row].SetContentFitted( false );
				_playerEntries[col, row].SetRow( row );
				_playerEntries[col, row].AddInputSignal( this );

				gridRow.SetEntry( col, 0, _playerEntries[col, row] );
			}

			gridRow.SetBgColor( 0, 0, 0, 255 );
			gridRow.SetSpacing( 0, 0 );
			gridRow.CopyColumnWidths( _headerGrid );
			gridRow.AutoSetRowHeights();
			gridRow.SetSize( PanelWidth( gridRow ), gridRow.CalcDrawHeight() );
			gridRow.RepositionContents();

			_playerList.AddItem( gridRow );
		}

		_hitTestPanel.SetBgColor( 0, 0, 0, 255 );
		_hitTestPanel.SetParent( this );
		_hitTestPanel.SetBounds( 0, 0, wide, tall );
		_hitTestPanel.AddInputSignal( this );

		_closeButton = new CommandButton( "x", (int)(wide - VguiUtil.XRes( 12 + 4 )), (int)VguiUtil.YRes( 2 ), (int)VguiUtil.XRes( 12 ), (int)VguiUtil.YRes( 12 ) );
		_closeButton.SetParent( this );
		_closeButton.AddActionSignal( new MenuHandlerStringCommandWatch( "-showscores", 1 ) );
		_closeButton.SetBgColor( 0, 0, 0, 255 );
		_closeButton.SetFgColor( 255, 255, 255, 0 );
		_closeButton.SetFont( titleFont );
		_closeButton.SetBoundKey( (char)255 );
		_closeButton.SetContentAlignment( Label.Alignment.Center );

		Initialize();
	}

	private static int PanelWidth( Panel panel )
	{
		panel.GetBounds( out _, out _, out var w, out _ );

		return w;
	}

	public void Initialize()
	{
		LastKilledBy = 0;
		LastKillTime = 0;
		PlayerNum = 0;
		NumTeams = 0;

		_hud.ClearPlayerExtraInfo();
		_hud.ClearTeamInfo();
	}

	public void Update()
	{
		_titleLabel.SetText( _hud.ViewPort.ServerName );

		Rows = 0;

		_hud.ViewPort.GetAllPlayersInfo();

		for ( var i = 0; i < NumRows; i++ )
		{
			SortedRows[i] = 0;
			IsATeam[i] = TeamNo;
		}

		for ( var i = 0; i < Hud.MaxPlayers; i++ )
			HasBeenSorted[i] = false;

		if ( _hud.Teamplay == 0 )
			SortPlayers( 0, null );
		else
			SortTeams();

		_playerList.SetScrollRange( Rows );

		FillGrid();

		_closeButton.SetVisible( _hud.ViewPort.SpectatorPanel.MenuVisible );
	}

	public void SortTeams()
	{
		var teams = _hud.TeamInfo;

		for ( var i = 1; i <= NumTeams; i++ )
		{
			if ( !teams[i].ScoresOverriden )
				teams[i].Frags = teams[i].Deaths = 0;

			teams[i].Ping = teams[i].PacketLoss = 0;
		}

		for ( var i = 1; i < Hud.MaxPlayers; i++ )
		{
			var player = _hud.PlayerInfoList[i];
			var extra = _hud.GetExtraPlayerInfo( i );

			if ( player.Name is null )
				continue;

			if ( extra.TeamName.Length == 0 )
				continue;

			int j;

			for ( j = 1; j <= NumTeams; j++ )
			{
				if ( string.Equals( extra.TeamName, teams[j].Name, StringComparison.OrdinalIgnoreCase ) )
					break;
			}

			if ( j > NumTeams )
				continue;

			if ( !teams[j].ScoresOverriden )
			{
				teams[j].Frags += extra.Frags;
				teams[j].Deaths += extra.Deaths;
			}

			teams[j].Ping += (short)player.Ping;
			teams[j].PacketLoss += (short)player.PacketLoss;
			teams[j].OwnTeam = (short)(player.ThisPlayer ? 1 : 0);
			teams[j].TeamNumber = extra.TeamNumber;
		}

		for ( var i = 1; i <= NumTeams; i++ )
		{
			teams[i].AlreadyDrawn = false;

			if ( teams[i].Players > 0 )
			{
				teams[i].Ping /= teams[i].Players;
				teams[i].PacketLoss /= teams[i].Players;
			}
		}

		while ( true )
		{
			var highestFrags = -99999;
			var lowestDeaths = 99999;
			var bestTeam = 0;

			for ( var i = 1; i <= NumTeams; i++ )
			{
				if ( teams[i].Players < 1 )
					continue;

				if ( !teams[i].AlreadyDrawn && teams[i].Frags >= highestFrags )
				{
					if ( teams[i].Frags > highestFrags || teams[i].Deaths < lowestDeaths )
					{
						bestTeam = i;
						lowestDeaths = teams[i].Deaths;
						highestFrags = teams[i].Frags;
					}
				}
			}

			if ( bestTeam == 0 )
				break;

			SortedRows[Rows] = bestTeam;
			IsATeam[Rows] = TeamYes;
			teams[bestTeam].AlreadyDrawn = true;
			Rows++;

			SortPlayers( 0, teams[bestTeam].Name );
		}

		SortPlayers( TeamSpectators, null );
	}

	public void SortPlayers( int team, string teamName )
	{
		var createdTeam = false;

		while ( true )
		{
			var highestFrags = -99999;
			var lowestDeaths = 99999;
			var bestPlayer = 0;

			for ( var i = 1; i < Hud.MaxPlayers; i++ )
			{
				var extra = _hud.GetExtraPlayerInfo( i );

				if ( HasBeenSorted[i] || _hud.PlayerInfoList[i].Name is null || extra.Frags < highestFrags )
					continue;

				var entity = _hud.Engine.GetEntityByIndex( i );

				if ( entity is null || (teamName is not null && !string.Equals( extra.TeamName, teamName, StringComparison.OrdinalIgnoreCase )) )
					continue;

				if ( extra.Frags > highestFrags || extra.Deaths < lowestDeaths )
				{
					bestPlayer = i;
					lowestDeaths = extra.Deaths;
					highestFrags = extra.Frags;
				}
			}

			if ( bestPlayer == 0 )
				break;

			if ( !createdTeam && team != 0 )
			{
				IsATeam[Rows] = team;
				Rows++;

				createdTeam = true;
			}

			SortedRows[Rows] = bestPlayer;
			HasBeenSorted[bestPlayer] = true;
			Rows++;
		}

		if ( teamName is not null )
			IsATeam[Rows++] = TeamBlank;
	}

	public void RebuildTeams()
	{
		var teams = _hud.TeamInfo;

		for ( var i = 1; i <= NumTeams; i++ )
			teams[i].Players = 0;

		_hud.ViewPort.GetAllPlayersInfo();

		NumTeams = 0;

		for ( var i = 1; i < Hud.MaxPlayers; i++ )
		{
			var extra = _hud.GetExtraPlayerInfo( i );

			if ( _hud.PlayerInfoList[i].Name is null )
				continue;

			if ( extra.TeamName.Length == 0 )
				continue;

			int j;

			for ( j = 1; j <= NumTeams; j++ )
			{
				if ( teams[j].Name.Length == 0 )
					break;

				if ( string.Equals( extra.TeamName, teams[j].Name, StringComparison.OrdinalIgnoreCase ) )
					break;
			}

			if ( j > NumTeams )
			{
				NumTeams = Math.Max( j, NumTeams );

				teams[j].Name = extra.TeamName;
				teams[j].Players = 0;
			}

			teams[j].Players++;
		}

		for ( var i = 1; i <= NumTeams; i++ )
		{
			if ( teams[i].Players < 1 )
				teams[i].Clear();
		}

		Update();
	}

	public void FillGrid()
	{
		var schemes = _hud.ViewPort.GetSchemeManager();
		var font = schemes.GetFont( schemes.GetSchemeHandle( "Scoreboard Text" ) );
		var titleFont = schemes.GetFont( schemes.GetSchemeHandle( "Scoreboard Title Text" ) );
		var smallFont = schemes.GetFont( schemes.GetSchemeHandle( "Scoreboard Small Text" ) );
		var dll = _hud.Dll;

		GetApp().GetCursorPos( out var x, out var y );
		CursorMoved( x, y, this );

		if ( !_hud.Voice.IsInSquelchMode() )
			HighlightRow = -1;

		for ( var row = 0; row < NumRows; row++ )
		{
			var gridRow = _playerGrids[row];

			gridRow.SetRowUnderline( 0, false, 0, 0, 0, 0, 0 );

			if ( row >= Rows )
			{
				for ( var col = 0; col < NumColumns; col++ )
					_playerEntries[col, row].SetVisible( false );

				continue;
			}

			for ( var col = 0; col < NumColumns; col++ )
			{
				var label = _playerEntries[col, row];

				label.SetVisible( true );
				label.SetText2( "" );
				label.SetImage( null );
				label.SetFont( font );
				label.SetTextOffset( 0, 0 );

				var rowHeight = VguiUtil.ScreenHeight > 480 ? (int)VguiUtil.YRes( 13 ) : 15;

				label.SetSize( label.GetWide(), rowHeight );
				label.SetBgColor( 0, 0, 0, 255 );

				HudPlayerInfo playerInfo = null;
				TeamInfo teamInfo = null;

				if ( IsATeam[row] == TeamBlank )
				{
					label.SetText( " " );
					continue;
				}

				if ( IsATeam[row] == TeamYes )
				{
					teamInfo = _hud.TeamInfo[SortedRows[row]];

					var color = teamInfo.TeamNumber % VguiUtil.NumberOfTeamColors;

					label.SetFgColor( VguiUtil.TeamColors[color, 0], VguiUtil.TeamColors[color, 1], VguiUtil.TeamColors[color, 2], 0 );

					rowHeight = 20;

					if ( VguiUtil.ScreenHeight >= 480 )
						rowHeight = (int)VguiUtil.YRes( rowHeight );

					label.SetSize( label.GetWide(), rowHeight );
					label.SetFont( titleFont );

					gridRow.SetRowUnderline( 0, true, (int)VguiUtil.YRes( 3 ), VguiUtil.TeamColors[color, 0], VguiUtil.TeamColors[color, 1], VguiUtil.TeamColors[color, 2], 0 );
				}
				else if ( IsATeam[row] == TeamSpectators )
				{
					label.SetFgColor( 100, 100, 100, 0 );

					rowHeight = 20;

					if ( VguiUtil.ScreenHeight >= 480 )
						rowHeight = (int)VguiUtil.YRes( rowHeight );

					label.SetSize( label.GetWide(), rowHeight );
					label.SetFont( titleFont );

					gridRow.SetRowUnderline( 0, true, (int)VguiUtil.YRes( 3 ), 100, 100, 100, 0 );
				}
				else
				{
					var color = _hud.GetExtraPlayerInfo( SortedRows[row] ).TeamNumber % VguiUtil.NumberOfTeamColors;

					label.SetFgColor( VguiUtil.TeamColors[color, 0], VguiUtil.TeamColors[color, 1], VguiUtil.TeamColors[color, 2], 0 );

					playerInfo = _hud.PlayerInfoList[SortedRows[row]];

					if ( playerInfo.ThisPlayer )
					{
						label.SetFgColor( SchemeColor.White );
						label.SetBgColor( VguiUtil.TeamColors[color, 0], VguiUtil.TeamColors[color, 1], VguiUtil.TeamColors[color, 2], 196 );
					}
					else if ( SortedRows[row] == LastKilledBy && LastKillTime != 0 && LastKillTime > _hud.Time )
					{
						label.SetBgColor( 255, 0, 0, (int)(255 - (15f * (LastKillTime - _hud.Time))) );
					}
				}

				if ( col == ColumnName || col == ColumnClass )
					label.SetContentAlignment( Label.Alignment.West );
				else if ( col == ColumnTracker )
					label.SetContentAlignment( Label.Alignment.Center );
				else
					label.SetContentAlignment( Label.Alignment.East );

				var text = "";

				if ( IsATeam[row] != 0 )
				{
					switch ( col )
					{
						case ColumnName:
							if ( IsATeam[row] == TeamSpectators )
								text = VguiUtil.Format( _hud.TextMessage.BufferedLocaliseTextString( "#Spectators" ) );
							else
								text = VguiUtil.Format( _hud.ViewPort.GetTeamName( teamInfo.TeamNumber ) );

							if ( IsATeam[row] == TeamYes )
							{
								var players = _hud.TextMessage.BufferedLocaliseTextString( teamInfo.Players == 1 ? "#Player" : "#Player_plural" );

								label.SetText2( $"({teamInfo.Players} {players})" );
								label.SetFont2( smallFont );
							}

							break;

						case ColumnKills:
							if ( IsATeam[row] == TeamYes )
								text = teamInfo.Frags.ToString();

							break;

						case ColumnDeaths:
							if ( IsATeam[row] == TeamYes )
								text = teamInfo.Deaths.ToString();

							break;

						case ColumnLatency:
							if ( IsATeam[row] == TeamYes )
								text = teamInfo.Ping.ToString();

							break;
					}
				}
				else
				{
					var extra = _hud.GetExtraPlayerInfo( SortedRows[row] );

					switch ( col )
					{
						case ColumnName:
							text = $"{playerInfo.Name}  ";
							break;

						case ColumnVoice:
							_hud.Voice.UpdateSpeakerImage( label, SortedRows[row] );
							break;

						case ColumnClass:
							var showClass = _hud.ViewPort is not null && HalfLifeClientDll.TfcIsAllyTeam( dll.TeamNumber, extra.TeamNumber ) != 0;

							if ( dll.TeamNumber == 0 )
								showClass = false;

							if ( showClass )
							{
								var noClass = extra.PlayerClass == 0 && _hud.ViewPort.GetValidClasses( extra.TeamNumber ) != -1;

								text = noClass ? "" : _hud.TextMessage.BufferedLocaliseTextString( TeamFortress.LocalisedClasses[extra.PlayerClass] );
							}

							break;

						case ColumnKills:
							text = extra.Frags.ToString();
							break;

						case ColumnDeaths:
							text = extra.Deaths.ToString();
							break;

						case ColumnLatency:
							text = playerInfo.Ping.ToString();
							break;
					}
				}

				label.SetText( text );
			}
		}

		for ( var row = 0; row < NumRows; row++ )
		{
			var gridRow = _playerGrids[row];

			gridRow.AutoSetRowHeights();
			gridRow.SetSize( PanelWidth( gridRow ), gridRow.CalcDrawHeight() );
			gridRow.RepositionContents();
		}

		_playerList.GetSize( out x, out y );
		_playerList.SetSize( x, y );
	}

	public void DeathMsg( int killer, int victim )
	{
		if ( victim == PlayerNum || killer == 0 )
		{
			LastKilledBy = killer != 0 ? killer : PlayerNum;
			LastKillTime = (int)(_hud.Time + 10);

			if ( killer == PlayerNum )
				LastKilledBy = PlayerNum;
		}
	}

	public void Open()
	{
		RebuildTeams();
		SetVisible( true );

		_hitTestPanel.SetVisible( true );
	}

	public void MouseOverCell( int row, int col )
	{
		var label = _playerEntries[col, row];

		if ( _currentHighlightLabel != label )
		{
			_currentHighlightLabel = null;
			HighlightRow = -1;
		}

		if ( IsATeam[row] != TeamNo )
			return;

		var playerInfo = _hud.PlayerInfoList[SortedRows[row]];

		if ( string.IsNullOrEmpty( playerInfo.Name ) )
			return;

		if ( playerInfo.ThisPlayer && !_hud.Engine.IsSpectateOnly )
			return;

		_currentHighlightLabel = label;
		HighlightRow = row;
	}

	public void MousePressed( MouseCode code, Panel panel )
	{
		if ( _hud.IntermissionActive )
			return;

		if ( !_hud.Voice.IsInSquelchMode() )
		{
			_hud.Voice.StartSquelchMode();
			_hitTestPanel.SetVisible( false );
		}
		else if ( HighlightRow >= 0 )
		{
			var player = SortedRows[HighlightRow];

			if ( player <= 0 )
				return;

			var playerInfo = _hud.PlayerInfoList[player];

			if ( string.IsNullOrEmpty( playerInfo.Name ) )
				return;

			if ( _hud.Voice.IsPlayerBlocked( player ) )
			{
				_hud.Voice.SetPlayerBlockedState( player, false );

				var string1 = VguiUtil.Format( _hud.TextMessage.BufferedLocaliseTextString( "#Unmuted" ), playerInfo.Name );

				_hud.TextMessage.TextMsg( HudTextMessage.HudPrintTalk, $"** {string1}\n" );
			}
			else
			{
				_hud.Voice.SetPlayerBlockedState( player, true );

				var string1 = VguiUtil.Format( _hud.TextMessage.BufferedLocaliseTextString( "#Muted" ), playerInfo.Name );
				var string2 = VguiUtil.Format( _hud.TextMessage.BufferedLocaliseTextString( "#No_longer_hear_that_player" ) );

				_hud.TextMessage.TextMsg( HudTextMessage.HudPrintTalk, $"** {string1} {string2}\n" );
			}
		}
	}

	public void CursorMoved( int x, int y, Panel panel )
	{
		if ( !_hud.Voice.IsInSquelchMode() )
			return;

		for ( var i = 0; i < NumRows; i++ )
		{
			if ( _playerGrids[i].GetCellAtPoint( x, y, out _, out var col ) )
			{
				MouseOverCell( i, col );
				return;
			}
		}
	}

	public void CursorEntered( Panel panel )
	{
	}

	public void CursorExited( Panel panel )
	{
	}

	public void MouseDoublePressed( MouseCode code, Panel panel )
	{
	}

	public void MouseReleased( MouseCode code, Panel panel )
	{
	}

	public void MouseWheeled( int delta, Panel panel )
	{
	}

	public void KeyPressed( KeyCode code, Panel panel )
	{
	}

	public void KeyTyped( KeyCode code, Panel panel )
	{
	}

	public void KeyReleased( KeyCode code, Panel panel )
	{
	}

	public void KeyFocusTicked( Panel panel )
	{
	}
}
