using Rhino.Geometry;

namespace RhinoLib
{
    public static class Test
    {
        public static string maintest ()
        {
            var c1 = new Circle(1);
            var c2 = new Circle(2);

            var inter = Curve.CreateBooleanIntersection(c1.ToNurbsCurve(), c2.ToNurbsCurve(), 0.0001);
            return "Sucess";
        }
    }
}
