using System;
using GoldSrc.Vgui1;

namespace GoldSrc;

partial class Client
{
	private sealed class EngineApp : App
	{
		public override void Main()
		{
		}
	}

	private sealed class EngineSurfaceWrap : SurfaceBase
	{
		private const int PageSize = 512;
		private const int MaxPages = 8;
		private const int CharCount = 256;
		private const int Transparent = 255;

		private sealed class FontPages
		{
			public readonly Texture[] Pages = new Texture[MaxPages];
			public readonly int[] Page = new int[CharCount];
			public readonly int[] X = new int[CharCount];
			public readonly int[] Y = new int[CharCount];
		}

		private readonly List<HudDraw> _draws = [];
		private readonly Dictionary<int, Texture> _textures = [];
		private readonly Dictionary<int, FontPages> _fontPages = [];
		private readonly BitmapTGA _emulatedCursorImage;
		private int _nextTextureId;
		private int _originX;
		private int _originY;
		private int _clipX0;
		private int _clipY0;
		private int _clipX1;
		private int _clipY1;
		private int _colorR;
		private int _colorG;
		private int _colorB;
		private int _colorA;
		private int _textColorR;
		private int _textColorG;
		private int _textColorB;
		private int _textColorA;
		private int _textX;
		private int _textY;
		private Texture _texture;
		private Font _font;
		private FontPages _pages;
		private bool _cursorLocked;

		public IReadOnlyList<HudDraw> Draws => _draws;

		public bool CursorVisible { get; private set; }

		public DefaultCursor CursorType { get; private set; } = DefaultCursor.Arrow;

		public Func<bool> GameUiActive { get; set; }

		public EngineSurfaceWrap( Panel embeddedPanel ) : base( embeddedPanel )
		{
			_cursorLocked = false;
			_emulatedCursorImage = new BitmapTGA( new FileInputStream( "valve/gfx/vgui/mouse.tga", false ), true );
		}

		public void ClearDraws() => _draws.Clear();

		public override void SetTitle( string title )
		{
		}

		public override bool SetFullscreenMode( int wide, int tall, int bpp ) => false;

		public override void SetWindowedMode()
		{
		}

		public override void SetAsTopMost( bool state )
		{
		}

		public override void CreatePopup( Panel embeddedPanel )
		{
		}

		public override bool HasFocus() => true;

		public override bool IsWithin( int x, int y ) => true;

		public override int CreateNewTextureId() => ++_nextTextureId;

		public override void GetMousePos( out int x, out int y )
		{
			x = (int)Mouse.Position.x;
			y = (int)Mouse.Position.y;
		}

		public override void DrawSetColor( int r, int g, int b, int a )
		{
			_colorR = r;
			_colorG = g;
			_colorB = b;
			_colorA = a;
		}

		private bool Clip( ref int x0, ref int y0, ref int x1, ref int y1 )
		{
			x0 = Math.Max( x0 + _originX, _clipX0 );
			y0 = Math.Max( y0 + _originY, _clipY0 );
			x1 = Math.Min( x1 + _originX, _clipX1 );
			y1 = Math.Min( y1 + _originY, _clipY1 );

			return x0 < x1 && y0 < y1;
		}

		private SpriteRect? Scissor( int x0, int y0, int x1, int y1 )
		{
			return x0 >= _clipX0 && y0 >= _clipY0 && x1 <= _clipX1 && y1 <= _clipY1 ? null : new SpriteRect( _clipX0, _clipX1, _clipY0, _clipY1 );
		}

		private Color32 DrawColor() => new( (byte)_colorR, (byte)_colorG, (byte)_colorB, (byte)~_colorA );

		public override void DrawFilledRect( int x0, int y0, int x1, int y1 )
		{
			if ( _colorA == Transparent )
				return;

			if ( Clip( ref x0, ref y0, ref x1, ref y1 ) )
				_draws.Add( new HudDraw( HudDrawMode.Normal, null, x0, y0, x1 - x0, y1 - y0, 0, 0, DrawColor(), null ) );
		}

		public override void DrawOutlinedRect( int x0, int y0, int x1, int y1 )
		{
			if ( _colorA == Transparent )
				return;

			DrawFilledRect( x0, y0, x1, y0 + 1 );
			DrawFilledRect( x0, y1 - 1, x1, y1 );
			DrawFilledRect( x0, y0 + 1, x0 + 1, y1 - 1 );
			DrawFilledRect( x1 - 1, y0 + 1, x1, y1 - 1 );
		}

		public override void DrawSetTextFont( Font font )
		{
			_font = font;

			if ( font is null )
				return;

			if ( _fontPages.TryGetValue( font.Id, out _pages ) )
				return;

			_pages = new FontPages();
			_fontPages[font.Id] = _pages;

			var buffer = new byte[PageSize * PageSize * 4];
			var x = 0;
			var y = 0;
			var page = 0;

			for ( var ch = 0; ch < CharCount; ch++ )
			{
				font.GetCharAbcWide( ch, out _, out var b, out _ );

				var tall = font.Tall;
				var right = b + 1 + x;

				if ( right > PageSize )
					y += 1 + tall;

				var charX = right <= PageSize ? x : 0;

				if ( tall + 1 + y > PageSize )
				{
					_pages.Pages[page] = Texture.Create( PageSize, PageSize, ImageFormat.RGBA8888 ).WithData( buffer ).Finish();

					page++;

					if ( page == MaxPages )
						return;

					Array.Clear( buffer );

					charX = 0;
					y = 0;
				}

				font.GetCharRgba( ch, charX, y, PageSize, PageSize, buffer );

				_pages.Page[ch] = page;
				_pages.X[ch] = charX;
				_pages.Y[ch] = y;

				x = b + charX + 1;
			}

			_pages.Pages[page] = Texture.Create( PageSize, PageSize, ImageFormat.RGBA8888 ).WithData( buffer ).Finish();
		}

		public override void DrawSetTextColor( int r, int g, int b, int a )
		{
			_textColorR = r;
			_textColorG = g;
			_textColorB = b;
			_textColorA = a;
		}

		public override void DrawSetTextPos( int x, int y )
		{
			_textX = x;
			_textY = y;
		}

		public override void DrawPrintText( string text, int textLen )
		{
			if ( text is null || _font is null || _pages is null )
				return;

			var x = _textX;
			var y = _textY;
			var tall = _font.Tall;

			for ( var i = 0; i < textLen; i++ )
			{
				var ch = i < text.Length ? (byte)text[i] : 0;

				_font.GetCharAbcWide( ch, out var a, out var b, out var c );

				x += a;

				DrawPrintChar( x, y, b, tall, _pages.Pages[_pages.Page[ch]], _pages.X[ch], _pages.Y[ch] );

				x += c + b;
			}

			DrawSetTextPos( x, y );
		}

		private void DrawPrintChar( int x, int y, int wide, int tall, Texture page, int left, int top )
		{
			if ( _textColorA == Transparent || page is null )
				return;

			var x0 = x + _originX;
			var y0 = y + _originY;
			var x1 = x0 + wide;
			var y1 = y0 + tall;

			if ( Math.Max( x0, _clipX0 ) >= Math.Min( x1, _clipX1 ) || Math.Max( y0, _clipY0 ) >= Math.Min( y1, _clipY1 ) )
				return;

			var color = new Color32( (byte)_textColorR, (byte)_textColorG, (byte)_textColorB, (byte)~_textColorA );

			_draws.Add( new HudDraw( HudDrawMode.Normal, page, x0, y0, wide, tall, left, top, color, Scissor( x0, y0, x1, y1 ) ) );
		}

		public override void DrawSetTextureRgba( int id, byte[] rgba, int wide, int tall )
		{
			var data = rgba;

			if ( data.Length != wide * tall * 4 )
			{
				data = new byte[wide * tall * 4];

				Array.Copy( rgba, data, Math.Min( rgba.Length, data.Length ) );
			}

			_texture = Texture.Create( wide, tall, ImageFormat.RGBA8888 ).WithData( data ).Finish();
			_textures[id] = _texture;
		}

		public override void DrawSetTexture( int id )
		{
			_texture = _textures.GetValueOrDefault( id );
		}

		public override void DrawTexturedRect( int x0, int y0, int x1, int y1 )
		{
			if ( _texture is null || _colorA == Transparent )
				return;

			x0 += _originX;
			y0 += _originY;
			x1 += _originX;
			y1 += _originY;

			if ( Math.Max( x0, _clipX0 ) >= Math.Min( x1, _clipX1 ) || Math.Max( y0, _clipY0 ) >= Math.Min( y1, _clipY1 ) )
				return;

			_draws.Add( new HudDraw( HudDrawMode.StretchNearest, _texture, x0, y0, x1 - x0, y1 - y0, 0, 0, DrawColor(), Scissor( x0, y0, x1, y1 ) ) );
		}

		public override void Invalidate( Panel panel )
		{
		}

		public override void EnableMouseCapture( bool state )
		{
		}

		public override void SetCursor( Cursor cursor )
		{
			if ( GameUiActive?.Invoke() == true || cursor is null || _cursorLocked )
				return;

			CursorVisible = true;

			_currentCursor = cursor;
			_emulatedCursor.SetImage( _emulatedCursorImage );

			var defaultCursor = cursor.GetDefaultCursor();

			if ( defaultCursor == DefaultCursor.None )
			{
				_emulatedCursor.SetImage( null );

				CursorVisible = false;
			}

			if ( defaultCursor is >= DefaultCursor.None and <= DefaultCursor.Hand )
				CursorType = defaultCursor;
		}

		public void LockCursor()
		{
			_cursorLocked = true;
		}

		public void UnlockCursor()
		{
			_cursorLocked = false;

			SetCursor( _currentCursor );
		}

		public override void SwapBuffers()
		{
		}

		public override void PushMakeCurrent( Panel panel, bool useInsets )
		{
			var left = 0;
			var top = 0;

			if ( useInsets )
				panel.GetInset( out left, out top, out _, out _ );

			panel.GetAbsExtents( out var x0, out var y0, out _, out _ );
			panel.GetClipRect( out _clipX0, out _clipY0, out _clipX1, out _clipY1 );

			_originX = left + x0;
			_originY = top + y0;
		}

		public override void PopMakeCurrent( Panel panel )
		{
		}

		public override void ApplyChanges()
		{
		}
	}

	private EngineApp _vguiApp;
	private Panel _vguiRoot;
	private EngineSurfaceWrap _vguiSurface;
	private bool _vguiGameUiActive;
	private int _vguiMouseX = -1;
	private int _vguiMouseY = -1;
	private readonly bool[] _vguiModifiers = new bool[VGuiModifierKeys.Length];

	public Panel VGuiGetPanel() => _vguiRoot;

	public bool VGuiCursorVisible => _vguiSurface is { CursorVisible: true };

	public string VGuiCursor
	{
		get
		{
			return _vguiSurface?.CursorType switch
			{
				DefaultCursor.IBeam => "text",
				DefaultCursor.Hourglass => "wait",
				DefaultCursor.Crosshair => "crosshair",
				DefaultCursor.Up => "ns-resize",
				DefaultCursor.SizeNwse => "sizenwse",
				DefaultCursor.SizeNesw => "sizenesw",
				DefaultCursor.SizeWe => "sizewe",
				DefaultCursor.SizeNs => "sizens",
				DefaultCursor.SizeAll => "move",
				DefaultCursor.No => "not-allowed",
				DefaultCursor.Hand => "pointer",
				_ => "arrow"
			};
		}
	}

	[SkipHotload]
	private static readonly string[] VGuiKeyNames =
	[
		"0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "a", "b",
		"c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n",
		"o", "p", "q", "r", "s", "t", "u", "v", "w", "x", "y", "z",
		"kp_0", "kp_1", "kp_2", "kp_3", "kp_4", "kp_5", "kp_6", "kp_7", "kp_8", "kp_9", "kp_divide", "kp_multiply",
		"kp_minus", "kp_plus", "kp_enter", "kp_del", "[", "]", "semicolon", "'", "`", ",", ".", "/",
		"\\", "-", "=", "enter", "space", "backspace", "tab", "capslock", "numlock", "escape", "scrolllock", "ins",
		"del", "home", "end", "pgup", "pgdn", "pause", "shift", "rshift", "alt", "ralt", "ctrl", "rctrl",
		"lwin", "rwin", "app", "uparrow", "leftarrow", "downarrow", "rightarrow", "f1", "f2", "f3", "f4", "f5",
		"f6", "f7", "f8", "f9", "f10", "f11", "f12"
	];

	[SkipHotload]
	private static readonly (string Key, MouseCode Code)[] VGuiMouseNames =
	[
		("mouse1", MouseCode.Left),
		("mouse2", MouseCode.Right),
		("mouse3", MouseCode.Middle),
		("mouse4", MouseCode.Left),
		("mouse5", MouseCode.Left)
	];

	private static MouseCode VGuiMouseCode( string button )
	{
		return button switch
		{
			"mousemiddle" => MouseCode.Middle,
			"mouseright" => MouseCode.Right,
			_ => MouseCode.Left
		};
	}

	public void VGuiMousePressed( string button )
	{
		if ( _vguiSurface is not null )
			_vguiApp.InternalMousePressed( VGuiMouseCode( button ), _vguiSurface );
	}

	public void VGuiMouseReleased( string button )
	{
		if ( _vguiSurface is not null )
			_vguiApp.InternalMouseReleased( VGuiMouseCode( button ), _vguiSurface );
	}

	public void VGuiMouseWheel( int delta )
	{
		if ( _vguiSurface is not null )
			_vguiApp.InternalMouseWheeled( delta, _vguiSurface );
	}

	[SkipHotload]
	private static readonly (string Key, Vgui1.KeyCode Code)[] VGuiModifierKeys =
	[
		("shift", Vgui1.KeyCode.LShift),
		("alt", Vgui1.KeyCode.LAlt),
		("ctrl", Vgui1.KeyCode.LControl),
		("rshift", Vgui1.KeyCode.LShift),
		("ralt", Vgui1.KeyCode.RAlt),
		("rctrl", Vgui1.KeyCode.RControl),
		("lwin", Vgui1.KeyCode.LWin),
		("rwin", Vgui1.KeyCode.RWin)
	];

	private void VGuiModifiers()
	{
		for ( var i = 0; i < VGuiModifierKeys.Length; i++ )
		{
			var down = Sandbox.Input.Keyboard.Down( VGuiModifierKeys[i].Key );

			if ( down == _vguiModifiers[i] )
				continue;

			_vguiModifiers[i] = down;

			if ( down )
				_vguiApp.InternalKeyPressed( VGuiModifierKeys[i].Code, _vguiSurface );
			else
				_vguiApp.InternalKeyReleased( VGuiModifierKeys[i].Code, _vguiSurface );
		}
	}

	private void VGuiInput()
	{
		var x = (int)Mouse.Position.x;
		var y = (int)Mouse.Position.y;

		if ( x != _vguiMouseX || y != _vguiMouseY )
		{
			_vguiMouseX = x;
			_vguiMouseY = y;

			_vguiApp.InternalCursorMoved( x, y, _vguiSurface );
		}

		if ( !GameUiActive && !VGuiCursorVisible )
		{
			for ( var i = 0; i < VGuiMouseNames.Length; i++ )
			{
				if ( Sandbox.Input.Keyboard.Pressed( VGuiMouseNames[i].Key ) )
					_vguiApp.InternalMousePressed( VGuiMouseNames[i].Code, _vguiSurface );
				else if ( Sandbox.Input.Keyboard.Released( VGuiMouseNames[i].Key ) )
					_vguiApp.InternalMouseReleased( VGuiMouseNames[i].Code, _vguiSurface );
			}

			var wheel = (int)Sandbox.Input.MouseWheel.y;

			if ( wheel != 0 )
				_vguiApp.InternalMouseWheeled( wheel, _vguiSurface );
		}

		for ( var i = 0; i < VGuiKeyNames.Length; i++ )
		{
			var code = (Vgui1.KeyCode)i;

			if ( Sandbox.Input.Keyboard.Pressed( VGuiKeyNames[i] ) )
			{
				_vguiApp.InternalKeyPressed( code, _vguiSurface );
				_vguiApp.InternalKeyTyped( code, _vguiSurface );
			}
			else if ( Sandbox.Input.Keyboard.Released( VGuiKeyNames[i] ) )
			{
				_vguiApp.InternalKeyReleased( code, _vguiSurface );
			}
			else
			{
				continue;
			}

			VGuiModifiers();
		}
	}

	private void VGuiStartup()
	{
		if ( _vguiSurface is not null )
			return;

		_vguiApp ??= new EngineApp();
		_vguiApp.Reset();

		_vguiRoot = new Panel( 0, 0, 320, 240 );
		_vguiRoot.SetPaintBorderEnabled( false );
		_vguiRoot.SetPaintBackgroundEnabled( false );
		_vguiRoot.SetPaintEnabled( false );
		_vguiRoot.SetCursor( _vguiApp.GetScheme().GetCursor( SchemeCursor.None ) );

		_vguiSurface = new EngineSurfaceWrap( _vguiRoot ) { GameUiActive = VGuiGameUiActive };

		VGuiSetBounds();
	}

	private bool VGuiGameUiActive() => GameUiActive;

	private void VGuiSetBounds()
	{
		_vguiRoot?.SetBounds( 0, 0, ScreenWidth, ScreenHeight );
	}

	private void VGuiPaint( bool paint )
	{
		if ( _vguiRoot is null )
			return;

		_vguiSurface.ClearDraws();

		if ( GameUiActive != _vguiGameUiActive )
		{
			_vguiGameUiActive = GameUiActive;

			if ( GameUiActive )
			{
				_vguiSurface.SetCursor( _vguiApp.GetScheme().GetCursor( SchemeCursor.Arrow ) );
				_vguiSurface.LockCursor();
			}
			else
			{
				_vguiSurface.UnlockCursor();
			}
		}

		VGuiInput();
		VGuiSetBounds();

		_vguiRoot.Repaint();
		_vguiApp.ExternalTick();

		if ( !paint )
			return;

		_vguiRoot.PaintTraverse();

		_hudDraws.AddRange( _vguiSurface.Draws );
	}
}
