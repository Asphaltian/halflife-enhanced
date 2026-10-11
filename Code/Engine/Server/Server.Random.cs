using System;

namespace GoldSrc;

partial class Server
{
	private const int RandomTableSize = 32;
	private const int RandomMultiplier = 16807;
	private const int RandomQuotient = 127773;
	private const int RandomModulus = int.MaxValue;
	private const int RandomDivisor = 1 + ((RandomModulus - 1) / RandomTableSize);
	private const double RandomScale = 4.656612875245797e-10;
	private const float RandomMax = 0.9999999f;

	private static int[] _randomTable = new int[RandomTableSize];

	private static int _randomSeed = SeedRandomNumberGenerator();
	private static int _randomLast;

	public static int RandomLong( int low, int high )
	{
		var range = (uint)(high - low) + 1;

		if ( range == 0 || (uint)(high - low) >= 0x80000000 )
			return low;

		var limit = 0x7fffffffu - (uint)(0x80000000 % range);
		uint value;

		do
		{
			value = (uint)NextRandom();
		}
		while ( value > limit );

		return low + (int)(value % range);
	}

	public static float RandomFloat( float low, float high )
	{
		var value = (float)(NextRandom() * RandomScale);

		if ( value > RandomMax )
			value = RandomMax;

		return ((high - low) * value) + low;
	}

	private static int SeedRandomNumberGenerator()
	{
		var seed = -(int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		if ( seed > 1000 )
			return -seed;

		if ( seed > -1000 )
			return seed - 22261048;

		return seed;
	}

	private static int NextRandom()
	{
		if ( _randomSeed <= 0 || _randomLast == 0 )
		{
			_randomSeed = -_randomSeed;

			if ( _randomSeed < 1 )
				_randomSeed = 1;

			for ( var i = RandomTableSize + 7; i >= 0; i-- )
			{
				_randomSeed = StepRandom( _randomSeed );

				if ( i < RandomTableSize )
					_randomTable[i] = _randomSeed;
			}

			_randomLast = _randomTable[0];
		}

		_randomSeed = StepRandom( _randomSeed );

		var index = _randomLast / RandomDivisor;

		_randomLast = _randomTable[index];
		_randomTable[index] = _randomSeed;

		return _randomLast;
	}

	private static int StepRandom( int seed )
	{
		seed = unchecked((seed * RandomMultiplier) - (seed / RandomQuotient * RandomModulus));

		if ( seed < 0 )
			seed += RandomModulus;

		return seed;
	}
}
