namespace GoldSrc.Vgui1;

public class LoweredBorder : Border
{
	public LoweredBorder() : base( 2, 2, 2, 2 )
	{
	}

	protected override void Paint( Panel panel )
	{
		panel.GetSize( out var wide, out var tall );

		DrawSetColor( SchemeColor.Secondary2 );
		DrawFilledRect( 0, 0, wide, 2 );
		DrawFilledRect( 0, 2, 2, tall );

		DrawSetColor( SchemeColor.White );
		DrawFilledRect( 1, tall - 2, wide, tall );
		DrawFilledRect( wide - 2, 1, wide, tall - 1 );

		DrawSetColor( SchemeColor.Secondary1 );
		DrawFilledRect( 1, 1, wide - 1, 2 );
		DrawFilledRect( 1, 2, 2, tall - 1 );
	}
}

public class RaisedBorder : Border
{
	public RaisedBorder() : base( 2, 2, 2, 2 )
	{
	}

	protected override void Paint( Panel panel )
	{
		panel.GetSize( out var wide, out var tall );

		DrawSetColor( SchemeColor.White );
		DrawFilledRect( 0, 0, wide, 2 );
		DrawFilledRect( 0, 2, 2, tall );

		DrawSetColor( SchemeColor.Secondary2 );
		DrawFilledRect( 2, tall - 2, wide, tall );
		DrawFilledRect( wide - 2, 2, wide, tall - 1 );

		DrawSetColor( SchemeColor.Secondary1 );
		DrawFilledRect( 1, tall - 1, wide, tall );
		DrawFilledRect( wide - 1, 1, wide, tall - 1 );
	}
}

public class EtchedBorder : Border
{
	public EtchedBorder() : base( 2, 2, 2, 2 )
	{
	}

	protected override void Paint( Panel panel )
	{
		panel.GetSize( out var wide, out var tall );

		DrawSetColor( SchemeColor.White );
		DrawFilledRect( 0, 0, wide, 2 );
		DrawFilledRect( 0, tall - 2, wide, tall );
		DrawFilledRect( 0, 2, 2, tall - 2 );
		DrawFilledRect( wide - 2, 2, wide, tall - 2 );

		DrawSetColor( SchemeColor.Secondary2 );
		DrawFilledRect( 0, 0, wide - 1, 1 );
		DrawFilledRect( 0, tall - 2, wide - 1, tall - 1 );
		DrawFilledRect( 0, 1, 1, tall - 2 );
		DrawFilledRect( wide - 2, 1, wide - 1, tall - 2 );
	}
}

public class BorderLayout : Layout
{
	public enum Alignment
	{
		Center,
		North,
		South,
		East,
		West
	}

	private sealed class Info( Alignment alignment ) : LayoutInfo
	{
		public readonly Alignment Alignment = alignment;

		public override LayoutInfo GetThis() => this;
	}

	private readonly int _inset;

	public BorderLayout( int inset )
	{
		_inset = inset;
	}

	public virtual LayoutInfo CreateLayoutInfo( Alignment alignment ) => new Info( alignment );

	public override void PerformLayout( Panel panel )
	{
		panel.GetSize( out var wide, out var tall );

		var maxNorth = 0;
		var maxSouth = 0;
		var maxEast = 0;
		var maxWest = 0;

		for ( var i = 0; i < panel.GetChildCount(); i++ )
		{
			if ( panel.GetChild( i ).GetLayoutInfo() is not Info info )
				continue;

			var child = panel.GetChild( i );

			switch ( info.Alignment )
			{
				case Alignment.North:
					if ( child.GetTall() > maxNorth )
						maxNorth = child.GetTall();

					break;
				case Alignment.South:
					if ( child.GetTall() > maxSouth )
						maxSouth = child.GetTall();

					break;
				case Alignment.East:
					if ( child.GetTall() > maxEast )
						maxEast = child.GetWide();

					break;
				case Alignment.West:
					if ( child.GetWide() > maxWest )
						maxWest = child.GetWide();

					break;
			}
		}

		var x0 = _inset + maxWest;
		var x1 = wide - _inset - maxEast;
		var y0 = _inset + maxNorth;
		var y1 = tall - _inset - maxSouth;

		for ( var i = 0; i < panel.GetChildCount(); i++ )
		{
			if ( panel.GetChild( i ).GetLayoutInfo() is not Info info )
				continue;

			var child = panel.GetChild( i );

			switch ( info.Alignment )
			{
				case Alignment.Center:
					child.SetBounds( x0, y0, x1 - x0, y1 - y0 );
					break;
				case Alignment.North:
					child.SetBounds( 0, 0, wide, y0 );
					break;
				case Alignment.South:
					child.SetBounds( 0, y1, wide, tall - y1 );
					break;
				case Alignment.East:
					child.SetBounds( x1, y0, wide - x1, y1 - y0 );
					break;
				case Alignment.West:
					child.SetBounds( 0, y0, x0, y1 - y0 );
					break;
			}
		}
	}
}

public class StackLayout : Layout
{
	private readonly int _vgap;
	private readonly bool _fitWide;

	public StackLayout( int vgap, bool fitWide )
	{
		_vgap = vgap;
		_fitWide = fitWide;
	}

	public override void PerformLayout( Panel panel )
	{
		var y = 0;

		for ( var i = 0; i < panel.GetChildCount(); i++ )
		{
			var child = panel.GetChild( i );

			child.GetBounds( out var x, out _, out _, out var tall );
			child.SetPos( x, y );

			if ( _fitWide )
			{
				panel.GetPaintSize( out var paintWide, out _ );
				child.SetSize( paintWide, tall );
			}

			y += tall + _vgap;
		}
	}
}

public abstract class InputStream
{
	public abstract void SeekStart( out bool success );
	public abstract void SeekRelative( int count, out bool success );
	public abstract void SeekEnd( out bool success );
	public abstract int GetAvailable( out bool success );
	public abstract byte ReadUChar( out bool success );
	public abstract void ReadUChar( byte[] buf, int count, out bool success );
	public abstract void Close( out bool success );
}

public class BitmapTGA : Bitmap
{
	private const int Uncompressed = 2;
	private const int RunLength = 10;
	private const int MaxPixels = 0x1fffffff;

	public BitmapTGA( InputStream stream, bool invertAlpha )
	{
		LoadTga( stream, invertAlpha );
	}

	private static int ReadUShort( InputStream stream, out bool success )
	{
		var buffer = new byte[2];

		stream.ReadUChar( buffer, 2, out success );

		return buffer[0] | (buffer[1] << 8);
	}

	private bool LoadTga( InputStream stream, bool invertAlpha )
	{
		if ( stream is null )
			return false;

		var idLength = stream.ReadUChar( out var success );

		if ( !success )
			return false;

		var colorMapType = stream.ReadUChar( out success );

		if ( !success )
			return false;

		var imageType = stream.ReadUChar( out success );

		if ( !success )
			return false;

		ReadUShort( stream, out success );

		if ( !success )
			return false;

		ReadUShort( stream, out success );

		if ( !success )
			return false;

		stream.ReadUChar( out success );

		if ( !success )
			return false;

		ReadUShort( stream, out success );

		if ( !success )
			return false;

		ReadUShort( stream, out success );

		if ( !success )
			return false;

		var wide = ReadUShort( stream, out success );

		if ( !success )
			return false;

		var tall = ReadUShort( stream, out success );

		if ( !success )
			return false;

		var pixelSize = stream.ReadUChar( out success );

		if ( !success )
			return false;

		stream.ReadUChar( out success );

		if ( !success )
			return false;

		if ( imageType != Uncompressed && imageType != RunLength )
			return false;

		if ( colorMapType != 0 )
			return false;

		if ( pixelSize != 24 && pixelSize != 32 )
			return false;

		if ( tall * wide > MaxPixels )
			return false;

		SetSize( wide, tall );

		if ( _rgba is null )
			return false;

		if ( idLength != 0 )
			stream.SeekRelative( idLength, out success );

		if ( imageType == Uncompressed )
		{
			for ( var y = tall - 1; y >= 0; y-- )
			{
				var pixel = y * wide * 4;

				for ( var x = 0; x < wide; x++ )
				{
					if ( pixelSize == 24 )
					{
						if ( !ReadColor( stream, pixel ) )
							return false;

						_rgba[pixel + 3] = 255;
					}
					else
					{
						if ( !ReadColor( stream, pixel ) )
							return false;

						var alpha = stream.ReadUChar( out success );

						_rgba[pixel + 3] = (byte)(255 - alpha);

						if ( !success )
							return false;

						if ( invertAlpha )
							_rgba[pixel + 3] = (byte)(255 - _rgba[pixel + 3]);
					}

					pixel += 4;
				}
			}

			return true;
		}

		var row = tall - 1;

		if ( row < 0 )
			return true;

		var column = 0;
		var position = row * wide * 4;

		while ( true )
		{
			if ( wide <= 0 )
			{
				row--;

				if ( row < 0 )
					return true;

				position = row * wide * 4;
				continue;
			}

			var packet = stream.ReadUChar( out success );

			if ( !success )
				return false;

			var count = (packet & 0x7f) + 1;

			if ( (packet & 0x80) != 0 )
			{
				byte blue = 0;
				byte green = 0;
				byte red = 0;
				byte alpha = 0;

				if ( pixelSize == 24 || pixelSize == 32 )
				{
					blue = stream.ReadUChar( out success );

					if ( !success )
						return false;

					green = stream.ReadUChar( out success );

					if ( !success )
						return false;

					red = stream.ReadUChar( out success );

					if ( !success )
						return false;

					if ( pixelSize == 24 )
					{
						alpha = (byte)(invertAlpha ? 0 : 255);
					}
					else
					{
						alpha = stream.ReadUChar( out success );

						if ( !success )
							return false;

						if ( invertAlpha )
							alpha = (byte)(255 - alpha);
					}
				}

				for ( var i = 0; i < count; i++ )
				{
					_rgba[position] = red;
					_rgba[position + 1] = green;
					_rgba[position + 2] = blue;
					_rgba[position + 3] = alpha;

					position += 4;
					column++;

					if ( column != wide )
						continue;

					column = 0;

					if ( row <= 0 )
						return true;

					row--;
					position = row * wide * 4;
				}
			}
			else
			{
				for ( var i = 0; i < count; i++ )
				{
					if ( !ReadColor( stream, position ) )
						return false;

					if ( pixelSize == 24 )
					{
						_rgba[position + 3] = (byte)(invertAlpha ? 0 : 255);
					}
					else
					{
						var alpha = stream.ReadUChar( out success );

						_rgba[position + 3] = alpha;

						if ( !success )
							return false;

						if ( invertAlpha )
							_rgba[position + 3] = (byte)(255 - alpha);
					}

					position += 4;
					column++;

					if ( column != wide )
						continue;

					column = 0;

					if ( row <= 0 )
						return true;

					row--;
					position = row * wide * 4;
				}
			}
		}
	}

	private bool ReadColor( InputStream stream, int position )
	{
		_rgba[position + 2] = stream.ReadUChar( out var success );

		if ( !success )
			return false;

		_rgba[position + 1] = stream.ReadUChar( out success );

		if ( !success )
			return false;

		_rgba[position] = stream.ReadUChar( out success );

		return success;
	}
}
