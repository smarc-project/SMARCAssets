using System;

namespace dji{
    public sealed class DiscreteTf
    {
        readonly double[] b; 
        readonly double[] aTail;
        readonly double invA0;
        readonly double[] pastU;
        readonly double[] pastY;


        public DiscreteTf(double[] bRaw, double[] a)
        {
            if (a.Length < 2)           throw new ArgumentException("Denominator must have order >= 1.");
            if (Math.Abs(a[0]) < 1e-12) throw new ArgumentException("a[0] must be nonzero.");
            if (bRaw.Length > a.Length) throw new ArgumentException("Improper: numerator longer than denominator.");

            int denLen = a.Length;

            b = PadNumerator(bRaw, denLen);
            aTail = a[1..];
            invA0 = 1 / a[0];
            pastU  = new double[denLen];      
            pastY  = new double[denLen - 1];
        }

        static double[] PadNumerator(double[] bRaw, int denLen)
        {
            int d = denLen - bRaw.Length;
            if (d < 0) throw new ArgumentException("Improper: numerator longer than denominator.");
            double[] bPad = new double[denLen];
            Array.Copy(bRaw, 0, bPad, d, bRaw.Length);
            return bPad;
        }

        static double DotProduct(double[] x, double[] y)
        {
            if (x.Length != y.Length)
                throw new ArgumentException($"Length mismatch: {x.Length} vs {y.Length}");
            double s = 0.0;
            for (int i = 0; i < x.Length; i++) s += x[i] * y[i];
            return s;
        }

        static void AppendBottom(double[] array, double newValue)
        {
            for (int i = array.Length - 2; i >= 0; i--) array[i + 1] = array[i];
            array[0] = newValue;
        }

        public double Step(double u)
        {
            AppendBottom(this.pastU, u);
            double y = (DotProduct(this.b, this.pastU) - DotProduct(this.aTail, this.pastY)) * this.invA0;
            AppendBottom(this.pastY, y);
            return y;
        }
    }
}
