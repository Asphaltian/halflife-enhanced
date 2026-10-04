using System;

namespace GoldSrc.GameUI;

public sealed class Border
{
	private readonly record struct Line( Color32 Color, int StartOffset, int EndOffset );

	private static readonly string[] SideNames = ["Left", "Top", "Right", "Bottom"];

	private readonly Line[][] _sides = new Line[4][];

	public int InsetLeft { get; }

	public int InsetTop { get; }

	public int InsetRight { get; }

	public int InsetBottom { get; }

	public Border( Scheme scheme, KeyValues settings )
	{
		var inset = settings.GetString( "inset", "0 0 0 0" ).Split( ' ', StringSplitOptions.RemoveEmptyEntries );

		InsetLeft = inset.Length > 0 ? Common.Atoi( inset[0] ) : 0;
		InsetTop = inset.Length > 1 ? Common.Atoi( inset[1] ) : 0;
		InsetRight = inset.Length > 2 ? Common.Atoi( inset[2] ) : 0;
		InsetBottom = inset.Length > 3 ? Common.Atoi( inset[3] ) : 0;

		for ( var side = 0; side < 4; side++ )
		{
			var keys = settings.FindKey( SideNames[side] )?.Keys;
			var lines = new Line[keys?.Count ?? 0];

			for ( var i = 0; i < lines.Length; i++ )
			{
				var offset = keys[i].GetString( "offset" ).Split( ' ', StringSplitOptions.RemoveEmptyEntries );

				lines[i] = new Line(
					scheme.GetColor( keys[i].GetString( "color" ), default ),
					offset.Length > 0 ? Common.Atoi( offset[0] ) : 0,
					offset.Length > 1 ? Common.Atoi( offset[1] ) : 0 );
			}

			_sides[side] = lines;
		}
	}

	public void Paint( Surface surface, int x0, int y0, int x1, int y1, int breakSide = -1, int breakStart = 0, int breakEnd = 0 )
	{
		for ( var i = 0; i < _sides[0].Length; i++ )
		{
			var line = _sides[0][i];

			surface.DrawSetColor( line.Color );

			if ( breakSide != 0 )
			{
				surface.DrawFilledRect( x0 + i, y0 + line.StartOffset, x0 + i + 1, y1 - line.EndOffset );
				continue;
			}

			if ( breakStart > 0 )
				surface.DrawFilledRect( x0 + i, y0 + line.StartOffset, x0 + i + 1, y0 + breakStart );

			if ( breakEnd < y1 - line.EndOffset )
				surface.DrawFilledRect( x0 + i, y0 + breakEnd + 1, x0 + i + 1, y1 - line.EndOffset );
		}

		for ( var i = 0; i < _sides[1].Length; i++ )
		{
			var line = _sides[1][i];

			surface.DrawSetColor( line.Color );

			if ( breakSide != 1 )
			{
				surface.DrawFilledRect( x0 + line.StartOffset, y0 + i, x1 - line.EndOffset, y0 + i + 1 );
				continue;
			}

			if ( breakStart > 0 )
				surface.DrawFilledRect( x0 + line.StartOffset, y0 + i, x0 + breakStart, y0 + i + 1 );

			if ( breakEnd < x1 - line.EndOffset )
				surface.DrawFilledRect( x0 + breakEnd + 1, y0 + i, x1 - line.EndOffset, y0 + i + 1 );
		}

		for ( var i = 0; i < _sides[2].Length; i++ )
		{
			var line = _sides[2][i];

			surface.DrawSetColor( line.Color );
			surface.DrawFilledRect( x1 - 1 - i, y0 + line.StartOffset, x1 - i, y1 - line.EndOffset );
		}

		for ( var i = 0; i < _sides[3].Length; i++ )
		{
			var line = _sides[3][i];

			surface.DrawSetColor( line.Color );
			surface.DrawFilledRect( x0 + line.StartOffset, y1 - 1 - i, x1 - line.EndOffset, y1 - i );
		}
	}
}
