using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace GoldSrc;

public sealed class SizeBuf
{
	private const int WordSlack = 8;

	internal static uint[] RowBitTable =
	[
		0x0, 0x1, 0x3, 0x7, 0xF, 0x1F, 0x3F, 0x7F, 0xFF, 0x1FF, 0x3FF, 0x7FF, 0xFFF, 0x1FFF, 0x3FFF, 0x7FFF, 0xFFFF,
		0x1FFFF, 0x3FFFF, 0x7FFFF, 0xFFFFF, 0x1FFFFF, 0x3FFFFF, 0x7FFFFF, 0xFFFFFF, 0x1FFFFFF, 0x3FFFFFF, 0x7FFFFFF,
		0xFFFFFFF, 0x1FFFFFFF, 0x3FFFFFFF, 0x7FFFFFFF, 0xFFFFFFFF
	];

	private readonly bool _allowOverflow;

	private int _outBit;
	private int _outByte;

	public string Name { get; set; }

	public byte[] Data { get; }

	public int MaxSize { get; }

	public int CurSize { get; set; }

	public bool Overflowed { get; private set; }

	public SizeBuf( string name, int maxSize, bool allowOverflow )
	{
		Name = name;
		MaxSize = maxSize;
		Data = new byte[maxSize + WordSlack];

		_allowOverflow = allowOverflow;
	}

	public void Clear()
	{
		Overflowed = false;
		CurSize = 0;
	}

	public void SetOverflowed() => Overflowed = true;

	public int GetSpace( int length )
	{
		if ( CurSize + length <= MaxSize )
		{
			var offset = CurSize;

			CurSize += length;

			return offset;
		}

		var name = Name ?? "???";

		if ( !_allowOverflow )
		{
			if ( MaxSize == 0 )
				throw new InvalidDataException( $"SZ_GetSpace:  Tried to write to an uninitialized sizebuf_t: {name}" );

			throw new InvalidDataException( $"SZ_GetSpace: overflow without FSB_ALLOWOVERFLOW set on {name}" );
		}

		if ( length > MaxSize )
			Con.DPrintf( $"SZ_GetSpace: {length} is > full buffer size on {name}, ignoring" );

		Con.Printf( $"SZ_GetSpace: overflow on {name}\n" );

		Overflowed = true;
		CurSize = length;

		return 0;
	}

	private bool InData( int offset, int length ) => offset >= 0 && offset + length <= Data.Length;

	public void Write( byte[] data, int length )
	{
		var offset = GetSpace( length );

		if ( !Overflowed && InData( offset, length ) )
			Array.Copy( data, 0, Data, offset, length );
	}

	public void Write( SizeBuf source ) => Write( source.Data, source.CurSize );

	public void WriteChar( int value ) => WriteByte( value );

	public void WriteByte( int value )
	{
		var offset = GetSpace( 1 );

		if ( InData( offset, 1 ) )
			Data[offset] = (byte)value;
	}

	public void WriteShort( int value )
	{
		var offset = GetSpace( 2 );

		if ( !InData( offset, 2 ) )
			return;

		Data[offset] = (byte)value;
		Data[offset + 1] = (byte)(value >> 8);
	}

	public void WriteWord( int value ) => WriteShort( value );

	public void WriteLong( int value )
	{
		var offset = GetSpace( 4 );

		if ( InData( offset, 4 ) )
			BinaryPrimitives.WriteInt32LittleEndian( Data.AsSpan( offset ), value );
	}

	public void WriteFloat( float value )
	{
		var offset = GetSpace( 4 );

		if ( !Overflowed && InData( offset, 4 ) )
			BinaryPrimitives.WriteInt32LittleEndian( Data.AsSpan( offset ), BitConverter.SingleToInt32Bits( value ) );
	}

	public void WriteString( string value )
	{
		var bytes = value is null ? [] : Encoding.Latin1.GetBytes( value );
		var offset = GetSpace( bytes.Length + 1 );

		if ( Overflowed || !InData( offset, bytes.Length + 1 ) )
			return;

		Array.Copy( bytes, 0, Data, offset, bytes.Length );

		Data[offset + bytes.Length] = 0;
	}

	public void WriteBuf( byte[] data, int length )
	{
		if ( data is not null )
			Write( data, length );
	}

	public void WriteAngle( float value ) => WriteByte( (int)((float)(value % 360.0) * 256f / 360f) );

	public void WriteHiresAngle( float value ) => WriteShort( (int)((float)(value % 360.0) * 65536f / 360f) & 0xFFFF );

	public void WriteCoord( float value ) => WriteShort( (int)(value * 8f) );

	public void WriteDeltaUsercmd( in UserCmd to, in UserCmd from )
	{
		var fromImage = new byte[UserCmd.Size];
		var toImage = new byte[UserCmd.Size];

		from.Write( fromImage );
		to.Write( toImage );

		StartBitWriting();
		Delta.LookupRegistration( "usercmd_t" ).WriteDelta( this, fromImage, toImage, true, 0.0 );
		EndBitWriting();
	}

	public void StartBitWriting()
	{
		_outBit = 0;
		_outByte = CurSize;
	}

	public void EndBitWriting()
	{
		if ( Overflowed )
			return;

		if ( InData( _outByte, 1 ) )
			Data[_outByte] &= (byte)(0xFF >> (8 - _outBit));

		GetSpace( 1 );

		_outBit = 0;
		_outByte = 0;
	}

	private uint ReadWord( int offset ) => InData( offset, 4 ) ? BinaryPrimitives.ReadUInt32LittleEndian( Data.AsSpan( offset ) ) : 0u;

	private void WriteWord( int offset, uint value )
	{
		if ( InData( offset, 4 ) )
			BinaryPrimitives.WriteUInt32LittleEndian( Data.AsSpan( offset ), value );
	}

	public void WriteOneBit( int value )
	{
		if ( _outBit >= 8 )
		{
			GetSpace( 1 );

			_outBit = 0;
			_outByte++;
		}

		if ( Overflowed )
			return;

		if ( InData( _outByte, 1 ) )
		{
			if ( value != 0 )
				Data[_outByte] |= (byte)(1 << _outBit);
			else
				Data[_outByte] &= (byte)~(1 << _outBit);
		}

		_outBit++;
	}

	public void WriteBits( uint data, int numBits )
	{
		if ( numBits < 32 && data >= (uint)(1 << numBits) )
			data = RowBitTable[numBits];

		var extra = 0;

		if ( _outBit >= 8 )
		{
			extra = 1;

			_outBit = 0;
			_outByte++;
		}

		var bits = _outBit + numBits;

		if ( bits <= 32 )
		{
			var byteCount = bits >> 3;
			var bitsLeft = bits & 7;

			if ( bitsLeft == 0 )
				byteCount--;

			GetSpace( byteCount + extra );

			if ( Overflowed )
				return;

			WriteWord( _outByte, (ReadWord( _outByte ) & RowBitTable[_outBit]) | (data << _outBit) );

			_outBit = bitsLeft == 0 ? 8 : bitsLeft;
			_outByte += byteCount;

			return;
		}

		GetSpace( extra + 4 );

		if ( Overflowed )
			return;

		WriteWord( _outByte, (ReadWord( _outByte ) & RowBitTable[_outBit]) | (data << _outBit) );

		var shift = 32 - _outBit;

		_outByte += 4;
		_outBit = bits & 7;

		WriteWord( _outByte, data >> shift );
	}

	public void WriteSBits( int data, int numBits )
	{
		if ( numBits < 32 )
		{
			var max = (1 << (numBits - 1)) - 1;

			if ( data > max )
				data = max;
			else if ( data < -max )
				data = -max;
		}

		WriteOneBit( data < 0 ? 1 : 0 );
		WriteBits( (uint)Math.Abs( data ), numBits - 1 );
	}

	public void WriteBitString( string value )
	{
		foreach ( var character in Encoding.Latin1.GetBytes( value ?? "" ) )
		{
			if ( character == 0 )
				break;

			WriteBits( character, 8 );
		}

		WriteBits( 0, 8 );
	}

	public void WriteBitData( byte[] data, int length )
	{
		for ( var i = 0; i < length; i++ )
			WriteBits( data[i], 8 );
	}

	public void WriteBitAngle( float angle, int numBits )
	{
		if ( numBits >= 32 )
			throw new InvalidDataException( "Can't write bit angle with 32 bits precision\n" );

		var shift = 1 << numBits;

		WriteBits( (uint)((int)((float)(angle % 360.0) * (float)shift) / 360 & (shift - 1)), numBits );
	}

	public void WriteBitCoord( float value )
	{
		var whole = (int)value;
		var intValue = Math.Abs( whole );
		var fractValue = Math.Abs( whole * 8 ) & 7;

		WriteOneBit( intValue );
		WriteOneBit( fractValue );

		if ( intValue == 0 && fractValue == 0 )
			return;

		WriteOneBit( value <= -0.125f ? 1 : 0 );

		if ( intValue != 0 )
			WriteBits( (uint)intValue, 12 );

		if ( fractValue != 0 )
			WriteBits( (uint)fractValue, 3 );
	}

	public void WriteBitVec3Coord( Vector3 value )
	{
		var x = value.x >= 0.125f || value.x <= -0.125f;
		var y = value.y >= 0.125f || value.y <= -0.125f;
		var z = value.z >= 0.125f || value.z <= -0.125f;

		WriteOneBit( x ? 1 : 0 );
		WriteOneBit( y ? 1 : 0 );
		WriteOneBit( z ? 1 : 0 );

		if ( x )
			WriteBitCoord( value.x );

		if ( y )
			WriteBitCoord( value.y );

		if ( z )
			WriteBitCoord( value.z );
	}
}

public sealed class MessageReader
{
	private const int MaxString = 0x1FFF;
	private const int MaxStringLine = 0x7FF;

	private byte[] _data = [];
	private int _size;

	private int _bitReadCount;
	private int _bytesRead;
	private int _inBit;
	private int _inByte;

	public int ReadCount { get; set; }

	public bool BadRead { get; set; }

	public int Size => _size;

	public byte[] Data => _data;

	public void BeginReading( byte[] data, int size )
	{
		_data = data;
		_size = size;

		ReadCount = 0;
		BadRead = false;
	}

	private byte At( int offset ) => offset >= 0 && offset < _data.Length ? _data[offset] : (byte)0;

	private uint WordAt( int offset ) => (uint)(At( offset ) | (At( offset + 1 ) << 8) | (At( offset + 2 ) << 16) | (At( offset + 3 ) << 24));

	public int ReadChar()
	{
		if ( ReadCount + 1 > _size )
		{
			BadRead = true;
			return -1;
		}

		return (sbyte)At( ReadCount++ );
	}

	public int ReadByte()
	{
		if ( ReadCount + 1 > _size )
		{
			BadRead = true;
			return -1;
		}

		return At( ReadCount++ );
	}

	public int ReadShort()
	{
		if ( ReadCount + 2 > _size )
		{
			BadRead = true;
			return -1;
		}

		var value = (short)(At( ReadCount ) | (At( ReadCount + 1 ) << 8));

		ReadCount += 2;

		return value;
	}

	public int ReadWord()
	{
		if ( ReadCount + 2 > _size )
		{
			BadRead = true;
			return -1;
		}

		var value = At( ReadCount ) | (At( ReadCount + 1 ) << 8);

		ReadCount += 2;

		return value;
	}

	public int ReadLong()
	{
		if ( ReadCount + 4 > _size )
		{
			BadRead = true;
			return -1;
		}

		var value = (int)WordAt( ReadCount );

		ReadCount += 4;

		return value;
	}

	public float ReadFloat()
	{
		var value = BitConverter.Int32BitsToSingle( (int)WordAt( ReadCount ) );

		ReadCount += 4;

		return value;
	}

	public bool ReadBuf( int length, byte[] target )
	{
		if ( ReadCount + length > _size )
		{
			BadRead = true;
			return false;
		}

		Array.Copy( _data, ReadCount, target, 0, length );

		ReadCount += length;

		return true;
	}

	private string ReadString( int max, bool line )
	{
		var builder = new StringBuilder();

		while ( true )
		{
			if ( ReadCount + 1 > _size )
			{
				BadRead = true;
				break;
			}

			var character = At( ReadCount++ );

			if ( character is 0xFF or 0 || (line && character == '\n') )
				break;

			builder.Append( (char)character );

			if ( builder.Length >= max )
				break;
		}

		return builder.ToString();
	}

	public string ReadString() => ReadString( MaxString, false );

	public string ReadStringLine() => ReadString( MaxStringLine, true );

	public float ReadAngle()
	{
		if ( ReadCount + 1 > _size )
		{
			BadRead = true;
			return 0f;
		}

		var value = (sbyte)At( ReadCount++ );

		return BadRead ? 0f : (float)(value * 1.40625);
	}

	public float ReadHiresAngle()
	{
		if ( ReadCount + 2 > _size )
		{
			BadRead = true;
			return 0f;
		}

		var value = (short)(At( ReadCount ) | (At( ReadCount + 1 ) << 8));

		ReadCount += 2;

		return BadRead ? 0f : (float)(value * 0.0054931640625);
	}

	public float ReadCoord()
	{
		if ( ReadCount + 2 > _size )
		{
			BadRead = true;
			return 0f;
		}

		var value = (short)(At( ReadCount ) | (At( ReadCount + 1 ) << 8));

		ReadCount += 2;

		return BadRead ? 0f : (float)(value * 0.125);
	}

	public Vector3 ReadPosition() => new( ReadCoord(), ReadCoord(), ReadCoord() );

	public UserCmd ReadDeltaUsercmd( in UserCmd from )
	{
		var fromImage = new byte[UserCmd.Size];
		var toImage = new byte[UserCmd.Size];

		from.Write( fromImage );
		from.Write( toImage );

		StartBitReading();
		Server.LookupDelta( "usercmd_t" ).ParseDelta( this, fromImage, toImage, 0.0 );
		EndBitReading();

		var to = UserCmd.Read( toImage );

		to.ViewAngles = Common.NormalizeAngles( to.ViewAngles );

		return to;
	}

	public void StartBitReading()
	{
		_bytesRead = 0;
		_inBit = 0;
		_inByte = ReadCount;
		_bitReadCount = ReadCount + 1;

		if ( ReadCount + 1 > _size )
			BadRead = true;
	}

	public void EndBitReading()
	{
		if ( _bitReadCount > _size )
			BadRead = true;

		ReadCount = _bitReadCount;

		_bytesRead = 0;
		_inBit = 0;
		_inByte = 0;
	}

	public int CurrentBit => _inBit + (_bytesRead * 8);

	public int ReadOneBit()
	{
		if ( BadRead )
			return 1;

		if ( _inBit >= 8 )
		{
			_bytesRead++;
			_bitReadCount++;
			_inByte++;
			_inBit = 0;
		}

		if ( _bitReadCount > _size )
		{
			BadRead = true;
			return 1;
		}

		return (At( _inByte ) >> _inBit++) & 1;
	}

	public uint ReadBits( int numBits )
	{
		if ( BadRead )
			return 1;

		if ( _inBit >= 8 )
		{
			_bitReadCount++;
			_bytesRead++;
			_inByte++;
			_inBit = 0;
		}

		var bits = _inBit + numBits;
		var word = WordAt( _inByte );
		uint result;

		if ( bits <= 32 )
		{
			result = SizeBuf.RowBitTable[numBits] & (word >> _inBit);

			var byteCount = bits >> 3;

			if ( (bits & 7) != 0 )
			{
				_inBit = bits & 7;
			}
			else
			{
				_inBit = 8;
				byteCount--;
			}

			_inByte += byteCount;
			_bitReadCount += byteCount;
			_bytesRead += byteCount;
		}
		else
		{
			var shift = _inBit;

			_inByte += 4;
			_bitReadCount += 4;
			_bytesRead += 4;
			_inBit = bits & 7;

			result = (word >> shift) | ((SizeBuf.RowBitTable[_inBit] & WordAt( _inByte )) << (32 - shift));
		}

		if ( _bitReadCount > _size )
		{
			BadRead = true;
			return 1;
		}

		return result;
	}

	public uint PeekBits( int numBits )
	{
		var bitReadCount = _bitReadCount;
		var bytesRead = _bytesRead;
		var inBit = _inBit;
		var inByte = _inByte;

		var value = ReadBits( numBits );

		_bitReadCount = bitReadCount;
		_bytesRead = bytesRead;
		_inBit = inBit;
		_inByte = inByte;

		return value;
	}

	public int ReadSBits( int numBits )
	{
		var sign = ReadOneBit();
		var value = (int)ReadBits( numBits - 1 );

		return sign != 0 ? -value : value;
	}

	public float ReadBitAngle( int numBits )
	{
		return (float)((double)(float)ReadBits( numBits ) * (360.0 / (1 << numBits)));
	}

	public string ReadBitString()
	{
		var builder = new StringBuilder();

		while ( !BadRead )
		{
			var character = (byte)ReadBits( 8 );

			if ( character == 0 )
				break;

			builder.Append( (char)character );
		}

		return builder.ToString();
	}

	public void ReadBitData( byte[] target, int length )
	{
		for ( var i = 0; i < length; i++ )
			target[i] = (byte)ReadBits( 8 );
	}

	public float ReadBitCoord()
	{
		var hasInt = ReadOneBit();
		var hasFract = ReadOneBit();

		if ( hasInt == 0 && hasFract == 0 )
			return 0f;

		var sign = ReadOneBit();
		var intValue = hasInt != 0 ? (int)ReadBits( 12 ) : 0;
		var fractValue = hasFract != 0 ? (int)ReadBits( 3 ) : 0;

		var value = (float)((fractValue * 0.125) + intValue);

		return sign != 0 ? -value : value;
	}

	public Vector3 ReadBitVec3Coord( Vector3 value )
	{
		var x = ReadOneBit();
		var y = ReadOneBit();
		var z = ReadOneBit();

		if ( x != 0 )
			value.x = ReadBitCoord();

		if ( y != 0 )
			value.y = ReadBitCoord();

		if ( z != 0 )
			value.z = ReadBitCoord();

		return value;
	}
}
