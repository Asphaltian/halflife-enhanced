using System;

namespace GoldSrc.GameUI;

public sealed class Surface( Vgui vgui )
{
	private readonly List<HudDraw> _draws = [];
	private readonly List<Panel> _popups = [];
	private readonly Dictionary<string, Texture> _textures = new( StringComparer.OrdinalIgnoreCase );

	private int _originX;
	private int _originY;
	private int _clipX0;
	private int _clipY0;
	private int _clipX1;
	private int _clipY1;
	private Color32 _color;
	private Color32 _textColor;
	private SchemeFont _textFont;
	private int _textX;
	private int _textY;

	public IReadOnlyList<HudDraw> Draws => _draws;

	public IReadOnlyList<Panel> Popups => _popups;

	public Panel EmbeddedPanel { get; set; }

	public Panel RestrictedPanel { get; private set; }

	public int ScreenWide { get; private set; }

	public int ScreenTall { get; private set; }

	public bool EscapeKeySupported { get; set; }

	public CursorCode Cursor { get; private set; } = CursorCode.Arrow;

	public void SetCursor( CursorCode cursor ) => Cursor = cursor;

	public static void PlaySound( string fileName ) => Client.Current?.PlaySoundByName( fileName, 1f );

	public void SetScreenSize( int wide, int tall )
	{
		ScreenWide = wide;
		ScreenTall = tall;
	}

	public Texture LoadImage( string name, bool forceReload = false )
	{
		if ( !forceReload && _textures.TryGetValue( name, out var texture ) )
			return texture;

		return _textures[name] = Server.FindMountPath( $"{name}.tga", ".vtex" ) is { } path ? Texture.Load( $"{path}.vtex" ) : LoadBitmap( $"{name}.bmp" );
	}

	private const int BitmapFileHeaderSize = 14;
	private const ushort BitmapMagic = 0x4d42;
	private const int BitmapPaletteOffset = 0x28;

	private static Texture LoadBitmap( string fileName )
	{
		if ( Server.ReadFile( fileName ) is not { Length: >= BitmapFileHeaderSize } file || BitConverter.ToUInt16( file, 0 ) != BitmapMagic )
			return null;

		var pixels = BitConverter.ToInt32( file, 10 );
		var wide = BitConverter.ToInt32( file, BitmapFileHeaderSize + 4 );
		var tall = BitConverter.ToInt32( file, BitmapFileHeaderSize + 8 );
		var stride = wide;

		if ( (wide & 3) != 0 )
			stride += (wide + 4) & 3;

		var data = new byte[wide * tall * 4];

		for ( var y = 0; y < tall; y++ )
		{
			for ( var x = 0; x < wide; x++ )
			{
				var color = BitmapFileHeaderSize + BitmapPaletteOffset + (file[pixels + (stride * (tall - y - 1)) + x] * 4);
				var pixel = ((wide * y) + x) * 4;

				data[pixel] = file[color + 2];
				data[pixel + 1] = file[color + 1];
				data[pixel + 2] = file[color];
				data[pixel + 3] = 255;
			}
		}

		return Texture.Create( wide, tall ).WithData( data ).Finish();
	}

	public void CreatePopup( Panel panel, bool keyboardInput, bool mouseInput )
	{
		if ( panel.Parent is null )
			panel.SetParentInternal( EmbeddedPanel );

		panel.IsPopup = true;

		if ( !_popups.Contains( panel ) )
			_popups.Add( panel );

		panel.KeyboardInputEnabled = keyboardInput;
		panel.MouseInputEnabled = mouseInput;
	}

	public void ReleasePanel( Panel panel )
	{
		_popups.Remove( panel );

		if ( panel == RestrictedPanel )
			RestrictedPanel = null;
	}

	public void MovePopupToFront( Panel panel )
	{
		if ( _popups.Count == 0 || _popups[^1] == panel || !_popups.Remove( panel ) )
			return;

		_popups.Add( panel );
	}

	public void MovePopupToBack( Panel panel )
	{
		_popups.Remove( panel );
		_popups.Insert( 0, panel );
	}

	public void BringToFront( Panel panel )
	{
		panel.MoveToFrontInternal();

		if ( panel.IsPopup )
			MovePopupToFront( panel );
	}

	public void SetTopLevelFocus( Panel panel )
	{
		for ( ; panel is not null; panel = panel.Parent )
		{
			if ( !panel.IsPopup )
				continue;

			BringToFront( panel );
			return;
		}
	}

	public void RestrictPaintToSinglePanel( Panel panel )
	{
		RestrictedPanel = panel;
		vgui.Input.SetAppModalSurface( panel );
	}

	public bool ShouldPaintChildPanel( Panel child )
	{
		if ( RestrictedPanel is not null && !child.HasParent( RestrictedPanel ) )
			return false;

		if ( child.IsPopup && _popups.Contains( child ) )
			child.Render = true;

		return !child.IsPopup;
	}

	public void ClearDraws() => _draws.Clear();

	public void PaintTraverse( Panel panel )
	{
		if ( !panel.Visible )
			return;

		if ( panel != EmbeddedPanel )
		{
			panel.PaintTraverse();
			return;
		}

		foreach ( var popup in _popups )
			popup.Render = false;

		(RestrictedPanel ?? panel).PaintTraverse();

		for ( var i = 0; i < _popups.Count; i++ )
		{
			if ( _popups[i].Render )
				_popups[i].PaintTraverse();
		}
	}

	public void PushMakeCurrent( Panel panel, bool useInsets )
	{
		_originX = panel.AbsX + (useInsets ? panel.InsetLeft : 0);
		_originY = panel.AbsY + (useInsets ? panel.InsetTop : 0);
		_clipX0 = panel.ClipX0;
		_clipY0 = panel.ClipY0;
		_clipX1 = panel.ClipX1;
		_clipY1 = panel.ClipY1;
	}

	public void DrawSetColor( Color32 color ) => _color = color;

	public void DrawFilledRect( int x0, int y0, int x1, int y1 )
	{
		x0 = Math.Max( x0 + _originX, _clipX0 );
		y0 = Math.Max( y0 + _originY, _clipY0 );
		x1 = Math.Min( x1 + _originX, _clipX1 );
		y1 = Math.Min( y1 + _originY, _clipY1 );

		if ( x0 < x1 && y0 < y1 && _color.a != 0 )
			_draws.Add( new HudDraw( HudDrawMode.Normal, null, x0, y0, x1 - x0, y1 - y0, 0, 0, _color, null ) );
	}

	public void DrawTexturedRect( Texture texture, int x0, int y0, int x1, int y1 )
	{
		if ( texture is null )
			return;

		x0 += _originX;
		y0 += _originY;
		x1 += _originX;
		y1 += _originY;

		if ( x0 < _clipX1 && x1 > _clipX0 && y0 < _clipY1 && y1 > _clipY0 )
			_draws.Add( new HudDraw( HudDrawMode.Stretch, texture, x0, y0, x1 - x0, y1 - y0, 0, 0, _color, Scissor( x0, y0, x1, y1 ) ) );
	}

	private SpriteRect? Scissor( int x0, int y0, int x1, int y1 )
	{
		return x0 >= _clipX0 && y0 >= _clipY0 && x1 <= _clipX1 && y1 <= _clipY1 ? null : new SpriteRect( _clipX0, _clipX1, _clipY0, _clipY1 );
	}

	public void DrawSetTextFont( SchemeFont font ) => _textFont = font;

	public void DrawSetTextColor( Color32 color ) => _textColor = color;

	public void DrawSetTextPos( int x, int y )
	{
		_textX = x;
		_textY = y;
	}

	public void DrawUnicodeChar( char character )
	{
		if ( _textFont is null || char.IsControl( character ) || character == ' ' )
			return;

		var face = _textFont.Face;
		var glyph = face.Glyph( character );
		var x0 = _textX + _originX + glyph.Offset;
		var y0 = _textY + _originY;
		var x1 = x0 + glyph.Width;
		var y1 = y0 + face.Tall;

		if ( x0 >= _clipX1 || x1 <= _clipX0 || y0 >= _clipY1 || y1 <= _clipY0 )
			return;

		_draws.Add( new HudDraw( _textFont.Additive ? HudDrawMode.Additive : HudDrawMode.Normal, face.Atlas, x0, y0, glyph.Width, face.Tall, glyph.Left, glyph.Top, _textColor, Scissor( x0, y0, x1, y1 ) ) );
	}

	public static int GetCharacterWidth( SchemeFont font, char character ) => font?.Face.Width( character ) ?? 0;

	public static int GetFontTall( SchemeFont font ) => font?.Face.Tall ?? 0;
}
