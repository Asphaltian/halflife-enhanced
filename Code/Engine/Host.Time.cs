namespace GoldSrc;

public static partial class Host
{
	private const double MaxFrameTime = 0.25;

	[ConVar( "sys_ticrate" )]
	public static float TicRate { get; set; } = 100f;

	public static double RealTime { get; private set; }

	public static double FrameTime { get; private set; }

	public static int FrameCount { get; set; }

	private static double _oldRealTime;

	public static void AdvanceTime( double frameTime )
	{
		RealTime += frameTime;
		FrameTime = frameTime;
	}

	public static bool FilterTime( float time )
	{
		RealTime += time;

		if ( TicRate > 0f && RealTime - _oldRealTime < 1.0 / (TicRate + 1f) )
			return false;

		FrameTime = RealTime - _oldRealTime;

		_oldRealTime = RealTime;

		if ( FrameTime > MaxFrameTime )
			FrameTime = MaxFrameTime;

		return true;
	}
}
