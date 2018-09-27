﻿using System.Collections.Generic;
using ISAAR.MSolve.Discretization.Integration.Points;
using ISAAR.MSolve.Discretization.Integration.Quadratures;
using ISAAR.MSolve.FEM.Entities;
using ISAAR.MSolve.FEM.Interpolation.Inverse;
using ISAAR.MSolve.FEM.Interpolation.Jacobians;
using ISAAR.MSolve.Geometry.Coordinates;
using ISAAR.MSolve.Geometry.Shapes;
using ISAAR.MSolve.Numerical.LinearAlgebra;

namespace ISAAR.MSolve.FEM.Interpolation
{
    /// <summary>
    /// Basic implementation of <see cref="IIsoparametricInterpolation2D"/>.
    /// Authors: Serafeim Bakalakos
    /// </summary>
    public abstract class IsoparametricInterpolation2DBase: IIsoparametricInterpolation2D
    {
        private readonly Dictionary<IQuadrature2D, Dictionary<GaussPoint2D, Vector>> cachedFunctionsAtGPs;
        private readonly Dictionary<IQuadrature2D, Dictionary<GaussPoint2D, Matrix2D>> cachedNaturalGradientsAtGPs;

        protected IsoparametricInterpolation2DBase(CellType2D cellType, int numFunctions)
        {
            this.CellType = cellType;
            this.NumFunctions = numFunctions;
            this.cachedFunctionsAtGPs = new Dictionary<IQuadrature2D, Dictionary<GaussPoint2D, Vector>>();
            this.cachedNaturalGradientsAtGPs = new Dictionary<IQuadrature2D, Dictionary<GaussPoint2D, Matrix2D>>();
        }

        /// <summary>
        /// See <see cref="IIsoparametricInterpolation2D.CellType"/>.
        /// </summary>
        public CellType2D CellType { get; }

        /// <summary>
        /// See <see cref="IIsoparametricInterpolation2D.NumFunctions"/>.
        /// </summary>
        public int NumFunctions { get; }

        /// <summary>
        /// See <see cref="IIsoparametricInterpolation2D.NodalNaturalCoordinates"/>.
        /// </summary>
        public abstract IReadOnlyList<NaturalPoint2D> NodalNaturalCoordinates { get; }

        /// <summary>
        /// See <see cref="IIsoparametricInterpolation2D.CreateInverseMappingFor(IReadOnlyList{Node2D})"/>.
        /// </summary>
        public abstract IInverseInterpolation2D CreateInverseMappingFor(IReadOnlyList<Node2D> nodes);

        /// <summary>
        /// See <see cref="IIsoparametricInterpolation2D.EvaluateAllAt(IReadOnlyList{Node2D}, NaturalPoint2D)"/>.
        /// </summary>
        public EvalInterpolation2D EvaluateAllAt(IReadOnlyList<Node2D> nodes, NaturalPoint2D naturalPoint)
        {
            double xi = naturalPoint.Xi;
            double eta = naturalPoint.Eta;
            var shapeFunctions = new Vector(EvaluateAt(xi, eta));
            var naturalShapeDerivatives = new Matrix2D(EvaluateGradientsAt(xi, eta));
            return new EvalInterpolation2D(shapeFunctions, naturalShapeDerivatives, 
                new IsoparametricJacobian2D(nodes, naturalShapeDerivatives));
        }

        /// <summary>
        /// See <see cref="IIsoparametricInterpolation2D.EvaluateAllAtGaussPoints(IReadOnlyList{Node2D}, IQuadrature2D)"/>.
        /// </summary>
        public Dictionary<GaussPoint2D, EvalInterpolation2D> EvaluateAllAtGaussPoints(IReadOnlyList<Node2D> nodes,
            IQuadrature2D quadrature)
        {
            // The shape functions and natural derivatives at each Gauss point are probably cached from previous calls
            Dictionary<GaussPoint2D, Vector> shapeFunctionsAtGPs = EvaluateFunctionsAtGaussPoints(quadrature);
            Dictionary<GaussPoint2D, Matrix2D> naturalShapeDerivativesAtGPs = EvaluateNaturalGradientsAtGaussPoints(quadrature);

            // Calculate the Jacobians and shape derivatives w.r.t. global cartesian coordinates at each Gauss point
            var interpolationsAtGPs = new Dictionary<GaussPoint2D, EvalInterpolation2D>();
            foreach (var gaussPoint in quadrature.IntegrationPoints)
            {
                interpolationsAtGPs[gaussPoint] = new EvalInterpolation2D(shapeFunctionsAtGPs[gaussPoint],
                    naturalShapeDerivativesAtGPs[gaussPoint], new IsoparametricJacobian2D(nodes, naturalShapeDerivativesAtGPs[gaussPoint]));
            }
            return interpolationsAtGPs;
        }

        /// <summary>
        /// See <see cref="IIsoparametricInterpolation2D.EvaluateFunctionsAt(NaturalPoint2D)"/>.
        /// </summary>
        public Vector EvaluateFunctionsAt(NaturalPoint2D naturalPoint)
            => new Vector(EvaluateAt(naturalPoint.Xi, naturalPoint.Eta));

        /// <summary>
        /// See <see cref="IIsoparametricInterpolation2D.EvaluateFunctionsAtGaussPoints(IQuadrature2D)"/>.
        /// </summary>
        public Dictionary<GaussPoint2D, Vector> EvaluateFunctionsAtGaussPoints(IQuadrature2D quadrature)
        {
            bool isCached = cachedFunctionsAtGPs.TryGetValue(quadrature,
                out Dictionary<GaussPoint2D, Vector> shapeFunctionsAtGPs);
            if (!isCached)
            {
                shapeFunctionsAtGPs = new Dictionary<GaussPoint2D, Vector>();
                foreach (var gaussPoint in quadrature.IntegrationPoints)
                {
                    shapeFunctionsAtGPs[gaussPoint] = new Vector(EvaluateAt(gaussPoint.Xi, gaussPoint.Eta));
                }
                cachedFunctionsAtGPs.Add(quadrature, shapeFunctionsAtGPs);
            }
            return shapeFunctionsAtGPs;
        }

        /// <summary>
        /// See <see cref="IIsoparametricInterpolation2D.EvaluateNaturalGradientsAt(NaturalPoint2D)".
        /// </summary>
        public Matrix2D EvaluateNaturalGradientsAt(NaturalPoint2D naturalPoint)
            => new Matrix2D(EvaluateGradientsAt(naturalPoint.Xi, naturalPoint.Eta));

        /// <summary>
        /// See <see cref="IIsoparametricInterpolation2D.EvaluateNaturalGradientsAtGaussPoints(IQuadrature2D)"/>.
        /// </summary>
        /// <param name="quadrature"></param>
        public Dictionary<GaussPoint2D, Matrix2D> EvaluateNaturalGradientsAtGaussPoints(IQuadrature2D quadrature)
        {
            bool isCached = cachedNaturalGradientsAtGPs.TryGetValue(quadrature,
                out Dictionary<GaussPoint2D, Matrix2D> naturalGradientsAtGPs);
            if (!isCached)
            {
                naturalGradientsAtGPs = new Dictionary<GaussPoint2D, Matrix2D>();
                foreach (var gaussPoint in quadrature.IntegrationPoints)
                {
                    naturalGradientsAtGPs[gaussPoint] = new Matrix2D(EvaluateGradientsAt(gaussPoint.Xi, gaussPoint.Eta));
                }
                cachedNaturalGradientsAtGPs.Add(quadrature, naturalGradientsAtGPs);
            }
            return naturalGradientsAtGPs;
        }

        /// <summary>
        /// See <see cref="IIsoparametricInterpolation2D.TransformNaturalToCartesian(IReadOnlyList{Node2D}, NaturalPoint2D)"/>.
        /// </summary>
        public CartesianPoint2D TransformNaturalToCartesian(IReadOnlyList<Node2D> nodes, NaturalPoint2D naturalPoint)
        {
            double[] shapeFunctionValues = EvaluateAt(naturalPoint.Xi, naturalPoint.Eta);
            double x = 0, y = 0;
            for (int i = 0; i < nodes.Count; ++i)
            {
                x += shapeFunctionValues[i] * nodes[i].X;
                y += shapeFunctionValues[i] * nodes[i].Y;
            }
            return new CartesianPoint2D(x, y);
        }

        /// <summary>
        /// Evaluate shape function at a given point expressed in the natural coordinate system. Each entry corresponds to a
        /// different shape function.
        /// </summary>
        /// <param name="xi">The coordinate of the point along local axis Xi.</param>
        /// <param name="eta">The coordinate of the point along local axis Eta.</param>
        protected abstract double[] EvaluateAt(double xi, double eta);

        /// <summary>
        /// Evaluate derivatives of shape functions with respect to natural coordinates at a given point expressed in the 
        /// natural coordinate system. Each row corresponds to a different shape function, column 0 corresponds to derivatives
        /// with respect to Xi coordinate and column 1 corresponds to derivatives with respect to Eta coordinate.
        /// </summary>
        /// <param name="xi">The coordinate of the point along local axis Xi.</param>
        /// <param name="eta">The coordinate of the point along local axis Eta.</param>
        protected abstract double[,] EvaluateGradientsAt(double xi, double eta);
    }
}
