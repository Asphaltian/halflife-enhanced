using System;
using GoldSrc.Bsp;

namespace GoldSrc;

public sealed class BrushModel : Model
{
	public override ModelType Type => ModelType.Brush;
	public File File { get; init; }
	public Hull[] Hulls { get; init; }
	public int SubModel { get; init; }

	public float Radius
	{
		get
		{
			var corner = new Vector3(
				MathF.Max( MathF.Abs( Mins.x ), MathF.Abs( Maxs.x ) ),
				MathF.Max( MathF.Abs( Mins.y ), MathF.Abs( Maxs.y ) ),
				MathF.Max( MathF.Abs( Mins.z ), MathF.Abs( Maxs.z ) ) );

			return MathLib.Length( corner );
		}
	}

	public static BrushModel[] Load( File file, string name, string mountPath )
	{
		var hull0 = Hull.MakeHull0( file );
		var models = new BrushModel[file.Models.Length];

		for ( var i = 0; i < models.Length; i++ )
		{
			var subModel = file.Models[i];

			models[i] = new BrushModel
			{
				Name = i == 0 ? name : $"*{i}",
				File = file,
				SubModel = i,
				Mins = subModel.Mins - 1f,
				Maxs = subModel.Maxs + 1f,
				Hulls =
				[
					new Hull
					{
						ClipNodes = hull0,
						Planes = file.Planes,
						FirstClipNode = subModel.HeadNode[0],
						LastClipNode = hull0.Length - 1
					},
					CreateClipHull( file, subModel, 1, new Vector3( -16f, -16f, -36f ), new Vector3( 16f, 16f, 36f ) ),
					CreateClipHull( file, subModel, 2, new Vector3( -32f, -32f, -32f ), new Vector3( 32f, 32f, 32f ) ),
					CreateClipHull( file, subModel, 3, new Vector3( -16f, -16f, -18f ), new Vector3( 16f, 16f, 18f ) )
				],
				RenderModel = Sandbox.Model.Load( i == 0 ? $"{mountPath}.vmdl" : $"{mountPath}/{i}.vmdl" )
			};
		}

		return models;
	}

	private static Hull CreateClipHull( File file, in Bsp.Model subModel, int hull, Vector3 mins, Vector3 maxs )
	{
		return new Hull
		{
			ClipNodes = file.ClipNodes,
			Planes = file.Planes,
			FirstClipNode = subModel.HeadNode[hull],
			LastClipNode = file.ClipNodes.Length - 1,
			ClipMins = mins,
			ClipMaxs = maxs,
			PhysicsBounds = new BBox( subModel.Mins, subModel.Maxs )
		};
	}
}
