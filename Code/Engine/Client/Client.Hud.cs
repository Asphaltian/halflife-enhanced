using System;

namespace GoldSrc;

public readonly record struct SpriteRect( int Left, int Right, int Top, int Bottom );

public sealed class ClientSprite
{
	public string Name { get; init; }
	public string Sprite { get; init; }
	public int Res { get; init; }
	public SpriteRect Rect { get; init; }
}

public struct ClientData
{
	public Vector3 Origin;
	public Vector3 ViewAngles;
	public int WeaponBits;
	public float Fov;
}

public enum HudDrawMode
{
	Normal,
	Holes,
	Additive,
	Fill,
	Blend,
	Modulate,
	Stretch,
	StretchNearest
}

public readonly record struct HudDraw( HudDrawMode Mode, Texture Texture, int X, int Y, int Width, int Height, int Left, int Top, Color32 Color, SpriteRect? Scissor );

partial class Client
{
	private const int MaxHudSprites = 256;

	private sealed class HudSprite
	{
		public string Name;
		public SpriteModel Model;
		public Texture[] Textures;
		public int FrameCount;
	}

	private readonly List<HudSprite> _hudSprites = [];
	private readonly List<HudDraw> _hudDraws = [];

	private HudSprite _currentHudSprite;
	private Color32 _hudSpriteColor;
	private SpriteRect? _hudScissor;
	private Vector2 _hudScreenSize;
	private int _crosshairSprite;
	private SpriteRect _crosshairRect;
	private (int R, int G, int B) _crosshairColor;

	[ConVar( "crosshair", ConVarFlags.Saved )]
	private static float Crosshair { get; set; } = 1f;

	public IReadOnlyList<HudDraw> HudDraws => _hudDraws;

	[ConVar( "showpause" )]
	private static float ShowPause { get; set; } = 1f;

	private Texture _pausePic;
	private bool _paused;
	private bool _gameUiActiveLastCommand;

	public int ScreenWidth => (int)Screen.Width;

	public int ScreenHeight => (int)Screen.Height;

	public int SpriteLoad( string name )
	{
		if ( string.IsNullOrEmpty( name ) )
			return 0;

		var index = _hudSprites.FindIndex( x => x.Name.Equals( name, StringComparison.OrdinalIgnoreCase ) );

		if ( index < 0 )
		{
			if ( _hudSprites.Count == MaxHudSprites )
				throw new InvalidOperationException( $"cannot allocate more than {MaxHudSprites} HUD sprites\n" );

			index = _hudSprites.Count;
			_hudSprites.Add( new HudSprite { Name = name } );
		}

		var sprite = _hudSprites[index];

		sprite.Model = ModelCache.ForName( name ) as SpriteModel;

		if ( sprite.Model is null )
			return 0;

		sprite.FrameCount = sprite.Model.Frames.Length;
		sprite.Textures = new Texture[sprite.FrameCount];

		return index + 1;
	}

	private HudSprite GetHudSprite( int sprite )
	{
		return sprite >= 1 && sprite <= _hudSprites.Count ? _hudSprites[sprite - 1] : null;
	}

	public SpriteModel GetSpritePointer( int sprite ) => GetHudSprite( sprite )?.Model;

	private static SpriteFrame GetSpriteFrame( HudSprite sprite, int frame )
	{
		return sprite?.Model is not null && frame >= 0 && frame < sprite.Model.Frames.Length ? sprite.Model.Frames[frame] : null;
	}

	public int SpriteFrames( int sprite ) => GetHudSprite( sprite )?.FrameCount ?? 0;

	public int SpriteHeight( int sprite, int frame )
	{
		return GetSpriteFrame( GetHudSprite( sprite ), frame ) is { } spriteFrame ? (int)(spriteFrame.Up - spriteFrame.Down) : 0;
	}

	public int SpriteWidth( int sprite, int frame )
	{
		return GetSpriteFrame( GetHudSprite( sprite ), frame ) is { } spriteFrame ? (int)(spriteFrame.Right - spriteFrame.Left) : 0;
	}

	public void SpriteSet( int sprite, int r, int g, int b )
	{
		if ( GetHudSprite( sprite ) is not { Model: not null } hudSprite )
			return;

		_currentHudSprite = hudSprite;
		_hudSpriteColor = new Color32( (byte)Math.Clamp( r, 0, 255 ), (byte)Math.Clamp( g, 0, 255 ), (byte)Math.Clamp( b, 0, 255 ), 255 );
	}

	public void SpriteDraw( int frame, int x, int y, SpriteRect? rect ) => DrawSpriteFrame( HudDrawMode.Normal, "SPR_Draw", frame, x, y, rect );

	public void SpriteDrawHoles( int frame, int x, int y, SpriteRect? rect ) => DrawSpriteFrame( HudDrawMode.Holes, "SPR_DrawHoles", frame, x, y, rect );

	public void SpriteDrawAdditive( int frame, int x, int y, SpriteRect? rect ) => DrawSpriteFrame( HudDrawMode.Additive, "SPR_DrawAdditive", frame, x, y, rect );

	private void DrawSpriteFrame( HudDrawMode mode, string function, int frame, int x, int y, SpriteRect? rect )
	{
		if ( _currentHudSprite is not { } sprite || x >= ScreenWidth || y >= ScreenHeight )
			return;

		if ( GetSpriteFrame( sprite, frame ) is not { } spriteFrame )
		{
			Log.Trace( $"Client.dll {function} error:  invalid frame" );
			return;
		}

		var frameWidth = (int)(spriteFrame.Right - spriteFrame.Left);
		var frameHeight = (int)(spriteFrame.Up - spriteFrame.Down);
		var width = frameWidth;
		var height = frameHeight;
		var left = 0;
		var top = 0;

		if ( rect is { } r && r.Left < r.Right && r.Top < r.Bottom )
		{
			var clipLeft = Math.Max( r.Left, 0 );
			var clipRight = Math.Min( r.Right, frameWidth );
			var clipTop = Math.Max( r.Top, 0 );
			var clipBottom = Math.Min( r.Bottom, frameHeight );

			if ( clipLeft < clipRight && clipTop < clipBottom )
			{
				left = clipLeft;
				top = clipTop;
				width = clipRight - clipLeft;
				height = clipBottom - clipTop;
			}
		}

		sprite.Textures[frame] ??= Texture.Load( $"{Server.GetMountPath( sprite.Name )}/{frame}.vtex" );

		_hudDraws.Add( new HudDraw( mode, sprite.Textures[frame], x, y, width, height, left, top, _hudSpriteColor, _hudScissor ) );
	}

	public void SpriteEnableScissor( int x, int y, int width, int height )
	{
		var left = x > ScreenWidth ? ScreenWidth : Math.Max( x, 0 );
		var top = y > ScreenHeight ? ScreenHeight : Math.Max( y, 0 );
		var clipWidth = width > ScreenWidth - left ? ScreenWidth - left : Math.Max( width, 0 );
		var clipHeight = height > ScreenHeight - top ? ScreenHeight - top : Math.Max( height, 0 );

		_hudScissor = new SpriteRect( left, left + clipWidth, top, top + clipHeight );
	}

	public void SpriteDisableScissor() => _hudScissor = null;

	public ClientSprite[] SpriteGetList( string fileName )
	{
		if ( Server.LoadMountText( fileName ) is not { } file )
			return null;

		var position = 0;
		var count = Common.Atoi( Common.Parse( file, ref position ) ?? "" );

		if ( count == 0 )
			return null;

		var list = new ClientSprite[count];

		for ( var i = 0; i < count; i++ )
		{
			var name = Truncate( Common.Parse( file, ref position ) );
			var res = Common.Atoi( Common.Parse( file, ref position ) ?? "" );
			var sprite = Truncate( Common.Parse( file, ref position ) );
			var left = Common.Atoi( Common.Parse( file, ref position ) ?? "" );
			var top = Common.Atoi( Common.Parse( file, ref position ) ?? "" );
			var width = Common.Atoi( Common.Parse( file, ref position ) ?? "" );
			var height = Common.Atoi( Common.Parse( file, ref position ) ?? "" );

			list[i] = new ClientSprite { Name = name, Res = res, Sprite = sprite, Rect = new SpriteRect( left, left + width, top, top + height ) };
		}

		return list;
	}

	private static string Truncate( string token )
	{
		token ??= "";

		return token.Length > 63 ? token[..63] : token;
	}

	public void FillRgba( int x, int y, int width, int height, int r, int g, int b, int a )
	{
		var color = new Color32( (byte)Math.Clamp( r, 0, 255 ), (byte)Math.Clamp( g, 0, 255 ), (byte)Math.Clamp( b, 0, 255 ), (byte)Math.Clamp( a, 0, 255 ) );

		_hudDraws.Add( new HudDraw( HudDrawMode.Fill, null, x, y, width, height, 0, 0, color, null ) );
	}

	public void SetCrosshair( int sprite, SpriteRect rect, int r, int g, int b )
	{
		_crosshairSprite = sprite;
		_crosshairRect = rect;
		_crosshairColor = (r, g, b);
	}

	private void DrawCrosshair( Vector3 viewAngles, float tanHalfX, float tanHalfY )
	{
		if ( _crosshairSprite == 0 || Crosshair == 0f )
			return;

		MathLib.AngleVectors( viewAngles + _crosshairAngle, out var direction, out _, out _ );
		MathLib.AngleVectors( viewAngles, out var forward, out var right, out var up );

		var depth = Vector3.Dot( direction, forward );
		var screenX = Vector3.Dot( direction, right ) / (depth * tanHalfX);
		var screenY = Vector3.Dot( direction, up ) / (depth * tanHalfY);

		var x = (int)((screenX * 0.5f * ScreenWidth) + 0.5f + (ScreenWidth / 2));
		var y = (int)((screenY * 0.5f * ScreenHeight) + 0.5f + (ScreenHeight / 2));

		SpriteSet( _crosshairSprite, _crosshairColor.R, _crosshairColor.G, _crosshairColor.B );
		SpriteDrawHoles( 0, x - ((_crosshairRect.Right - _crosshairRect.Left) / 2), y - ((_crosshairRect.Bottom - _crosshairRect.Top) / 2), _crosshairRect );
	}

	public void ServerCmd( string command )
	{
		Cmd.TokenizeString( command );
		Cmd.ForwardToServer();
	}

	private readonly SizeBuf _stringCommand = new( "", ServerClient.MessageSize, true );

	public void ServerCmdUnreliable( string command )
	{
		Cmd.TokenizeString( command );

		if ( !CanForwardToServer )
		{
			if ( !Cmd.Argv( 0 ).Equals( "setinfo", System.StringComparison.OrdinalIgnoreCase ) )
				Log.Info( $"Can't \"{Cmd.Argv( 0 )}\", not connected" );

			return;
		}

		ForwardToServer( _datagram );
	}

	public void ForwardToServer() => ForwardToServer( _netchan.Message );

	private void ForwardToServer( SizeBuf target )
	{
		if ( _demoPlayback )
			return;

		var buffer = _stringCommand;

		buffer.Clear();
		buffer.WriteByte( Server.StringCommand );

		var text = Cmd.Argc <= 1 ? "\n" : Cmd.Args;

		if ( !Cmd.Argv( 0 ).Equals( "cmd", System.StringComparison.OrdinalIgnoreCase ) )
			text = $"{Cmd.Argv( 0 )} {text}";

		buffer.WriteString( text );

		if ( buffer.Overflowed )
			return;

		if ( buffer.CurSize + target.CurSize <= target.MaxSize )
			target.Write( buffer );
	}

	public static void ClientCmd( string command ) => Cmd.AddText( $"{command}\n" );

	private void UpdateClientData()
	{
		if ( _demoPlayback )
			return;

		var data = new ClientData
		{
			Origin = GetEntity( _viewEntity )?.Origin ?? default,
			ViewAngles = ViewAngles,
			WeaponBits = _weapons,
			Fov = FieldOfView
		};

		var recorded = data;

		if ( _clientDll.UpdateClientData( ref data, (float)ClientTime ) )
		{
			ViewAngles = data.ViewAngles;
			FieldOfView = data.Fov;
		}

		if ( _demoRecording )
			WriteDemoClientData( recorded );
	}

	private void HudRedraw( bool intermission, Vector3 viewAngles, float tanHalfX, float tanHalfY )
	{
		_currentHudSprite = null;
		_hudScissor = null;

		_hudDraws.Clear();

		PolyBlend();

		var size = new Vector2( ScreenWidth, ScreenHeight );

		if ( size != _hudScreenSize )
		{
			_hudScreenSize = size;

			VGuiSetBounds();

			_clientDll.VidInit();
		}

		if ( !GameUiActive )
			_clientDll.Redraw( (float)ClientTime, intermission );

		DrawDebugArea();
		DrawMessageMode();

		DrawCrosshair( viewAngles, tanHalfX, tanHalfY );

		DrawShowFps();
		DrawNetGraph();

		if ( ShowPause != 0f && _paused && !GameUiActive && !_gameUiActiveLastCommand )
		{
			_pausePic ??= Texture.Load( $"{Server.GetMountPath( "gfx/paused" )}.vtex" );

			_hudDraws.Add( new HudDraw( HudDrawMode.Holes, _pausePic, (ScreenWidth / 2) - (_pausePic.Width / 2), (ScreenHeight / 2) - (_pausePic.Height / 2), _pausePic.Width, _pausePic.Height, 0, 0, Color32.White, null ) );
		}

		CheckDrawCenterString( _intermission != 0 );
	}
}
