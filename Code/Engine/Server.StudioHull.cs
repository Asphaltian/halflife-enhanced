namespace GoldSrc;

partial class Server
{
	public const int TraceSimpleBox = 1 << 0;

	[ConVar( "sv_clienttrace", ConVarFlags.Replicated )]
	private static float SvClientTrace { get; set => Server.SetServerCvar( "sv_clienttrace", ref field, value ); } = 1f;

	private static readonly byte[] PlayerHullController = [0x7f, 0x7f, 0x7f, 0x7f];

	private readonly StudioHulls _studioHulls = new();
	private readonly byte[] _playerHullBlending = new byte[2];

	private static int StudioPlayerBlend( in Studio.SequenceDesc sequence, ref float pitch )
	{
		var blend = (float)(int)(pitch * 3f);

		if ( blend < sequence.BlendStart[0] )
		{
			pitch = (float)(pitch - (sequence.BlendStart[0] / 3.0));
			return 0;
		}

		if ( blend > sequence.BlendEnd[0] )
		{
			pitch = (float)(pitch - (sequence.BlendEnd[0] / 3.0));
			return 255;
		}

		var range = sequence.BlendEnd[0] - sequence.BlendStart[0];

		pitch = 0f;

		return range < 0.1 ? 127 : (int)((blend - sequence.BlendStart[0]) * 255f / range);
	}

	private int HullForStudioModel( Edict edict, Studio.File header, Vector3 mins, Vector3 maxs, out Hull hull, out Vector3 offset )
	{
		var size = maxs - mins;
		var useComplexHull = false;
		var factor = 0.5f;

		if ( size.x == 0f && size.y == 0f && size.z == 0f && (Globals.TraceFlags & TraceSimpleBox) == 0 )
		{
			useComplexHull = true;

			if ( (edict.EntityFlags & EntityFlags.Client) == EntityFlags.Client )
			{
				if ( SvClientTrace == 0f )
				{
					useComplexHull = false;
				}
				else
				{
					size = Vector3.One;
					factor = SvClientTrace * 0.5f;
				}
			}
		}

		if ( (header.Header.Flags & StudioHulls.TraceHitBox) == 0 && !useComplexHull )
		{
			hull = HullForEntity( edict, mins, maxs, out offset );
			return 1;
		}

		size *= factor;
		offset = Vector3.Zero;
		hull = _studioHulls[0];

		if ( (edict.EntityFlags & EntityFlags.Client) != EntityFlags.Client )
			return _studioHulls.Build( header, edict.Frame, edict.Sequence, edict.Angles, edict.Origin, size, edict.Controller, edict.Blending );

		var angles = edict.Angles;
		var pitch = angles.x;

		_playerHullBlending[0] = (byte)StudioPlayerBlend( header.Sequences[edict.Sequence], ref pitch );
		_playerHullBlending[1] = 0;

		angles.x = pitch;

		return _studioHulls.Build( header, edict.Frame, edict.Sequence, angles, edict.Origin, size, PlayerHullController, _playerHullBlending );
	}
}
