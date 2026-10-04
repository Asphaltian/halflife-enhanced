using System;
using GoldSrc.Bsp;

namespace GoldSrc;

partial class Server
{
	public string TraceTexture( Edict edict, Vector3 start, Vector3 end )
	{
		int firstNode;

		if ( edict is null )
		{
			firstNode = 0;
		}
		else
		{
			if ( GetModel( edict.ModelIndex ) is null )
				return null;

			var hull = HullForBsp( edict, Vector3.Zero, Vector3.Zero, out var offset );
			firstNode = hull.FirstClipNode;

			start -= offset;
			end -= offset;

			if ( edict.Angles.x != 0f || edict.Angles.y != 0f || edict.Angles.z != 0f )
			{
				MathLib.AngleVectors( edict.Angles, out var forward, out var right, out var up );

				start = new Vector3( Vector3.Dot( start, forward ), -Vector3.Dot( start, right ), Vector3.Dot( start, up ) );
				end = new Vector3( Vector3.Dot( end, forward ), -Vector3.Dot( end, right ), Vector3.Dot( end, up ) );
			}
		}

		if ( WorldModel.Nodes.Length == 0 )
			return null;

		var face = WorldModel.SurfaceAtPoint( firstNode, start, end );
		return face < 0 ? null : WorldModel.GetTextureName( WorldModel.Faces[face] );
	}
}
