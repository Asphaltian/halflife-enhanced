using System;
using System.Buffers.Binary;

namespace GoldSrc;

partial class Client
{
	private void Snapshot()
	{
		var baseName = "Snapshot";

		if ( _entities.Count != 0 && GetEntity( 0 ) is { } world && GetModelByIndex( world.ModelIndex ) is { } model )
			baseName = Common.FileBase( model.Name );

		for ( var i = 0; i < 1000; i++ )
		{
			var fileName = $"{baseName}{i:D4}.bmp";

			if ( FileSystem.Data.FileExists( fileName ) )
				continue;

			TakeSnapshot( fileName );

			return;
		}

		Con.Printf( "Unable to take a screenshot.\n" );
	}

	private void TakeSnapshot( string fileName )
	{
		var width = (int)Screen.Width;
		var height = (int)Screen.Height;
		var pixelCount = width * height;

		using var bitmap = new Bitmap( width, height );

		Scene.Camera.RenderToBitmap( bitmap );

		var source = bitmap.GetPixels32();
		var pixels = new byte[(pixelCount * 3) + 4];

		for ( var y = 0; y < height; y++ )
		{
			var from = (height - 1 - y) * width;
			var to = y * width * 3;

			for ( var x = 0; x < width; x++ )
			{
				var color = source[from + x];

				pixels[to++] = color.b;
				pixels[to++] = color.g;
				pixels[to++] = color.r;
			}
		}

		var rowSize = width * 3;
		var padding = (4 - (rowSize % 4)) % 4;
		var data = new byte[54 + ((rowSize + padding) * height)];

		data[0] = (byte)'B';
		data[1] = (byte)'M';

		BinaryPrimitives.WriteInt32LittleEndian( data.AsSpan( 2 ), (pixelCount + 0x12) * 3 );
		BinaryPrimitives.WriteInt32LittleEndian( data.AsSpan( 10 ), 54 );
		BinaryPrimitives.WriteInt32LittleEndian( data.AsSpan( 14 ), 40 );
		BinaryPrimitives.WriteInt32LittleEndian( data.AsSpan( 18 ), width );
		BinaryPrimitives.WriteInt32LittleEndian( data.AsSpan( 22 ), height );
		BinaryPrimitives.WriteInt16LittleEndian( data.AsSpan( 26 ), 1 );
		BinaryPrimitives.WriteInt16LittleEndian( data.AsSpan( 28 ), 24 );

		var write = 54;

		for ( var y = 0; y < height; y++ )
		{
			Array.Copy( pixels, y * rowSize, data, write, rowSize + padding );

			write += rowSize + padding;
		}

		FileSystem.Data.WriteAllBytes( fileName, data );
	}

	[ConCmd( "snapshot" )]
	private static void SnapshotCommand() => Current?.Snapshot();
}
