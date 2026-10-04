using GoldSrc;
using GoldSrc.Vgui1;

namespace HalfLife;

public static class VguiLoadTga
{
	private sealed class MemoryInputStream : InputStream
	{
		public byte[] Data;
		public int DataLength;
		public int ReadPosition;

		public override void SeekStart( out bool success )
		{
			ReadPosition = 0;
			success = true;
		}

		public override void SeekRelative( int count, out bool success )
		{
			ReadPosition += count;
			success = true;
		}

		public override void SeekEnd( out bool success )
		{
			ReadPosition = DataLength;
			success = true;
		}

		public override int GetAvailable( out bool success )
		{
			success = false;

			return 0;
		}

		public override byte ReadUChar( out bool success )
		{
			if ( ReadPosition >= 0 && ReadPosition < DataLength )
			{
				success = true;

				return Data[ReadPosition++];
			}

			success = false;

			return 0;
		}

		public override void ReadUChar( byte[] buf, int count, out bool success )
		{
			success = false;

			for ( var i = 0; i < count; i++ )
				buf[i] = ReadUChar( out success );
		}

		public override void Close( out bool success )
		{
			success = false;
			Data = null;
			DataLength = ReadPosition = 0;
		}
	}

	private static BitmapTGA Load( string fileName, bool invertAlpha )
	{
		var stream = new MemoryInputStream { Data = Server.ReadFile( fileName ) };

		if ( stream.Data is null )
			return null;

		stream.DataLength = stream.Data.Length;
		stream.ReadPosition = 0;

		return new BitmapTGA( stream, invertAlpha );
	}

	public static BitmapTGA LoadTga( string fileName ) => Load( fileName, true );

	public static BitmapTGA LoadTgaNoInvertAlpha( string fileName ) => Load( fileName, false );
}
