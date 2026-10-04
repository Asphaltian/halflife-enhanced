namespace GoldSrc.Vgui1;

public class Image
{
	private int _x;
	private int _y;
	private protected int _wide;
	private protected int _tall;
	private Panel _panel;
	private Color _color;

	public Image()
	{
		_x = 0;
		_y = 0;
		_wide = 0;
		_tall = 0;
		_color = new Color( 255, 255, 255, 0 );
	}

	public virtual void SetPos( int x, int y )
	{
		_x = x;
		_y = y;
	}

	public virtual void GetPos( out int x, out int y )
	{
		x = _x;
		y = _y;
	}

	public virtual void GetSize( out int wide, out int tall )
	{
		wide = _wide;
		tall = _tall;
	}

	public virtual void SetColor( Color color )
	{
		_color = color;
	}

	public virtual void GetColor( out Color color )
	{
		color = _color;
	}

	public virtual void SetSize( int wide, int tall )
	{
		_wide = wide;
		_tall = tall;
	}

	protected virtual void DrawSetColor( SchemeColor sc ) => _panel.DrawSetColor( sc );

	protected virtual void DrawSetColor( int r, int g, int b, int a ) => _panel.DrawSetColor( r, g, b, a );

	protected virtual void DrawFilledRect( int x0, int y0, int x1, int y1 ) => _panel.DrawFilledRect( _x + x0, _y + y0, x1 + _x, y1 + _y );

	protected virtual void DrawOutlinedRect( int x0, int y0, int x1, int y1 ) => _panel.DrawOutlinedRect( _x + x0, _y + y0, x1 + _x, y1 + _y );

	protected virtual void DrawSetTextFont( SchemeFont sf ) => _panel.DrawSetTextFont( sf );

	protected virtual void DrawSetTextFont( Font font ) => _panel.DrawSetTextFont( font );

	protected virtual void DrawSetTextColor( SchemeColor sc ) => _panel.DrawSetTextColor( sc );

	protected virtual void DrawSetTextColor( int r, int g, int b, int a ) => _panel.DrawSetTextColor( r, g, b, a );

	protected virtual void DrawSetTextPos( int x, int y ) => _panel.DrawSetTextPos( _x + x, _y + y );

	protected virtual void DrawPrintText( string text, int textLen ) => _panel.DrawPrintText( text, textLen );

	protected virtual void DrawPrintText( int x, int y, string text, int textLen ) => _panel.DrawPrintText( _x + x, _y + y, text, textLen );

	protected virtual void DrawPrintChar( char ch ) => _panel.DrawPrintChar( ch );

	protected virtual void DrawPrintChar( int x, int y, char ch ) => _panel.DrawPrintChar( _x + x, _y + y, ch );

	protected virtual void DrawSetTextureRgba( int id, byte[] rgba, int wide, int tall ) => _panel.DrawSetTextureRgba( id, rgba, wide, tall );

	protected virtual void DrawSetTexture( int id ) => _panel.DrawSetTexture( id );

	protected virtual void DrawTexturedRect( int x0, int y0, int x1, int y1 ) => _panel.DrawTexturedRect( x0, y0, x1, y1 );

	protected virtual void Paint( Panel panel )
	{
	}

	public virtual void DoPaint( Panel panel )
	{
		_panel = panel;
		Paint( panel );
		_panel = null;
	}
}

public class Bitmap : Image
{
	private int _id;
	private bool _uploaded;

	protected byte[] _rgba;

	public override void SetSize( int wide, int tall )
	{
		base.SetSize( wide, tall );

		_rgba = new byte[wide * tall * 4];
	}

	protected virtual void SetRgba( int x, int y, byte r, byte g, byte b, byte a )
	{
		if ( _rgba is null )
			return;

		GetSize( out var wide, out var tall );

		if ( x < 0 || x >= wide || y < 0 || y >= tall )
			return;

		var index = ((wide * y) + x) * 4;

		_rgba[index] = r;
		_rgba[index + 1] = g;
		_rgba[index + 2] = b;
		_rgba[index + 3] = a;
	}

	protected override void Paint( Panel panel )
	{
		if ( _rgba is null )
			return;

		GetSize( out var wide, out var tall );

		if ( _id == 0 )
		{
			if ( panel.GetSurfaceBase() is null )
				return;

			_id = panel.GetSurfaceBase().CreateNewTextureId();
		}

		if ( !_uploaded )
		{
			DrawSetTextureRgba( _id, _rgba, wide, tall );
			_uploaded = true;
		}

		GetColor( out var color );
		color.GetColor( out var r, out var g, out var b, out var a );

		DrawSetTexture( _id );
		DrawSetColor( r, g, b, a );

		GetPos( out var x, out var y );

		DrawTexturedRect( x, y, x + wide, y + tall );
	}
}

public class Border : Image
{
	protected int _left;
	protected int _top;
	protected int _right;
	protected int _bottom;

	public Border()
	{
	}

	public Border( int left, int top, int right, int bottom )
	{
		_left = left;
		_top = top;
		_right = right;
		_bottom = bottom;
	}

	public virtual void SetInset( int left, int top, int right, int bottom )
	{
		_left = left;
		_top = top;
		_right = right;
		_bottom = bottom;
	}

	public virtual void GetInset( out int left, out int top, out int right, out int bottom )
	{
		left = _left;
		top = _top;
		right = _right;
		bottom = _bottom;
	}

	protected override void DrawFilledRect( int x0, int y0, int x1, int y1 ) => base.DrawFilledRect( x0 - _left, y0 - _top, x1 - _left, y1 - _top );

	protected override void DrawOutlinedRect( int x0, int y0, int x1, int y1 ) => base.DrawOutlinedRect( x0 - _left, y0 - _top, x1 - _left, y1 - _top );

	protected override void DrawSetTextPos( int x, int y ) => base.DrawSetTextPos( x - _left, y - _top );

	protected override void DrawPrintText( int x, int y, string text, int textLen ) => base.DrawPrintText( x - _left, y - _top, text, textLen );

	protected override void DrawPrintChar( int x, int y, char ch ) => base.DrawPrintChar( x - _left, y - _top, ch );
}
