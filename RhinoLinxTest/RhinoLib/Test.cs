using Rhino.Geometry;
using System;
using System.IO;
using System.Reflection;

namespace RhinoLib
{
    public static class Test
    {
		
		private const string DefaultRhinoSystemDirectory = "/usr/lib/rhino3d";
		private const double Width = 10.0;
		private const double Depth = 20.0;
		private const double Height = 30.0;
		private const double ExpectedVolume = Width * Depth * Height;
		private const double Tolerance = 1e-8;


        public static string maintest ()
        {
            var c1 = new Circle(1);
            var c2 = new Circle(2);

            var inter = Curve.CreateBooleanIntersection(c1.ToNurbsCurve(), c2.ToNurbsCurve(), 0.0001);
			
			Console.WriteLine("Tested curve");
			
			using var brep = CreateValidatedBox(out var metrics);
			
			Console.WriteLine("Tested brep");
			var testnull = brep == null;
			Console.WriteLine($"brep is null: {testnull}");
            return "Sucess";
        }
	
			
			
		private static Brep CreateValidatedBox(out BoxMetrics metrics)
		{
			Brep? brep = null;

			try
			{
				var polyline = new Polyline(new[]
				{
					new Point3d(0, 0, 0),
					new Point3d(Width, 0, 0),
					new Point3d(Width, Depth, 0),
					new Point3d(0, Depth, 0),
					new Point3d(0, 0, 0)
				});

				if (!polyline.IsValid || !polyline.IsClosed || polyline.Count != 5)
				{
					throw new BoxSolverException(
						BoxSolverExitCode.GeometryValidation,
						"The rectangular polyline profile is invalid or not closed.");
				}

				using var profile = new PolylineCurve(polyline);
				if (!profile.IsValid || !profile.IsClosed || !profile.TryGetPlane(out _))
				{
					throw new BoxSolverException(
						BoxSolverExitCode.GeometryValidation,
						"The polyline profile is not a valid closed planar curve.");
				}

				using var extrusion = Extrusion.Create(profile, Height, cap: true)
					?? throw new BoxSolverException(
						BoxSolverExitCode.GeometryValidation,
						"Rhino could not create a capped extrusion from the polyline profile.");

				brep = extrusion.ToBrep()
					?? throw new BoxSolverException(
						BoxSolverExitCode.GeometryValidation,
						"Rhino could not convert the extrusion to a Brep.");

				if (!brep.IsValid || !brep.IsSolid)
				{
					throw new BoxSolverException(
						BoxSolverExitCode.GeometryValidation,
						"The generated Brep is invalid or is not a solid.");
				}

				var bounds = brep.GetBoundingBox(accurate: true);
				var size = bounds.Max - bounds.Min;
				var massProperties = VolumeMassProperties.Compute(brep)
					?? throw new BoxSolverException(
						BoxSolverExitCode.GeometryValidation,
						"Rhino could not calculate volume mass properties for the generated Brep.");

				if (!NearlyEqual(size.X, Width) ||
					!NearlyEqual(size.Y, Depth) ||
					!NearlyEqual(size.Z, Height) ||
					!NearlyEqual(massProperties.Volume, ExpectedVolume))
				{
					throw new BoxSolverException(
						BoxSolverExitCode.GeometryValidation,
						$"The generated Brep did not match the expected {Width} x {Depth} x {Height} dimensions and {ExpectedVolume} volume.");
				}

				metrics = new BoxMetrics(size.X, size.Y, size.Z, massProperties.Volume, brep.IsSolid);
				var completedBrep = brep;
				brep = null;
				return completedBrep!;
			}
			catch (BoxSolverException)
			{
				brep?.Dispose();
				throw;
			}
			catch (Exception exception)
			{
				brep?.Dispose();
				throw new BoxSolverException(
					BoxSolverExitCode.GeometryValidation,
					"Unable to create or validate the box extrusion.",
					exception);
			}
		
		}
		
		private static bool NearlyEqual(double actual, double expected) =>
			Math.Abs(actual - expected) <= Tolerance;
		
		
		public sealed class BoxSolverException : Exception
		{
			public BoxSolverException(BoxSolverExitCode exitCode, string message, Exception? innerException = null)
				: base(message, innerException)
			{
				BoxSolverExitCode = exitCode;
			}

			public BoxSolverExitCode BoxSolverExitCode { get; }
		}
 

		private sealed record RunSummary(
			string RhinoSystemDirectory,
			string RhinoVersion,
			bool IsSolid,
			double[] BoundingBox,
			double Volume,
			string Output);

		public enum BoxSolverExitCode
		{
			Success = 0,
			Unexpected = 1,
			Preflight = 2,
			RhinoStartup = 3,
			GeometryValidation = 4,
			OutputValidation = 5
		}
		
		private sealed record BoxMetrics(double Width, double Depth, double Height, double Volume, bool IsSolid);
	}
}
