﻿using System;
using System.Collections.Generic;
using ISAAR.MSolve.Solvers.Interfaces;
using System.IO;
using System.Diagnostics;
using ISAAR.MSolve.Numerical.LinearAlgebra;
using ISAAR.MSolve.Numerical.LinearAlgebra.Interfaces;

namespace ISAAR.MSolve.Solvers.PCG
{
    public class SolverPCG : IIterativeSolver
    {
        private bool internalVectorsAllocated = false;
        protected int iterations = 0;
        protected int currentIteration = 0;
        protected double detf;
        protected IIterativeSolverInitializer initializer;
        protected IIterativeSolverPreconditioner preconditioner;
        protected IIterativeSolverMatrixCalculator matrixCalculator;
        protected ISearchVectorCalculator search;
        private IVectorOLD x, r, p, z, q;
        protected readonly List<double> errors = new List<double>();

        public SolverPCG(IIterativeSolverInitializer initializer, IIterativeSolverMatrixCalculator matrixCalculator, IIterativeSolverPreconditioner preconditioner,
            ISearchVectorCalculator search)
        {
            this.initializer = initializer;
            this.matrixCalculator = matrixCalculator;
            this.preconditioner = preconditioner;
            this.search = search;

            StreamWriter sw = File.CreateText(String.Format(@"iterationsPCG-{0}.txt", Process.GetCurrentProcess().Id));
            sw.Close();
        }

        #region Properties
        public int CurrentIteration
        {
            get { return currentIteration; }
        }

        public IVectorOLD VectorP
        {
            get { return p; }
        }

        //public Vector<double> VectorW
        //{
        //    get { return w; }
        //}

        public IVectorOLD VectorZ
        {
            get { return z; }
        }

        public IVectorOLD VectorX
        {
            get { return x; }
        }

        public IVectorOLD VectorQ
        {
            get { return q; }
        }

        public IVectorOLD VectorR
        {
            get { return r; }
        }

        #endregion

        public void Initialize()
        {
            throw new InvalidOperationException("Iterative solvers cannot use this method.");
        }

        public void Initialize(IVectorOLD x, IVectorOLD residual, double detf)
        {
            this.x = x;
            this.r = residual;
            this.detf = initializer.InitializeAndGetResidual(this.r, this.x);

            if (!internalVectorsAllocated)
            {
                int iLagr = x.Length;
                //int iLagr = matrixCalculator.VectorSize;
                p = new Vector(iLagr);
                //w = new Vector<double>(iLagr);
                z = new Vector(iLagr);
                //y = new Vector<double>(iLagr);
                q = new Vector(iLagr);
                internalVectorsAllocated = true;
            }
            else
            {
                p.Clear();
                //w.Clear();
                z.Clear();
                //y.Clear();
                q.Clear();
            }
        }

        public void Solve(int maxIterations, double tolerance)
        {
            int iLagr = x.Length;
            iterations = 0;
            //matrixCalculator.Projectr(r, w);
            //matrixCalculator.Precondition(w, z);
            //matrixCalculator.Projectz(z, y);
            if (r.Norm > 1e-25)
            {
                preconditioner.Precondition(r, z);

                errors.Clear();
                for (currentIteration = 0; currentIteration < maxIterations; currentIteration++)
                {
                    search.CalculateSearchVector(this);
                    matrixCalculator.MultiplyWithMatrix(p, q);
                    double a = search.CalculateGradient(this);
                    double detz = 0;
                    //Parallel.For(0, iLagr, i =>
                    for (int i = 0; i < iLagr; i++)
                    {
                        r[i] -= a * q[i];
                        x[i] += a * p[i];
                        detz += r[i] * r[i];
                    };
                    errors.Add(Math.Sqrt(detz) / detf);
                    if (errors[currentIteration] < tolerance) break;

                    //matrixCalculator.Projectr(r, w);
                    //matrixCalculator.Precondition(w, z);
                    //matrixCalculator.Projectz(z, y);
                    preconditioner.Precondition(r, z);
                }
                iterations = currentIteration;
            }

            //StreamWriter sw = File.CreateText(@"iterationsPCG.txt");
            //for (int i = 0; i < errors.Count; i++)
            //    sw.WriteLine(errors[i].ToString());
            //sw.Close();
            
            StreamWriter sw = File.AppendText(String.Format(@"iterationsPCG-{0}.txt", Process.GetCurrentProcess().Id));
            sw.WriteLine(iterations.ToString());
            sw.Close();
        }

        public void Solve()
        {
            Solve(1000, 1e-6);
        }
    }
}
