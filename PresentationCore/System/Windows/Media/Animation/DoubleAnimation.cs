//---------------------------------------------------------------------------- 
//
// <copyright file="DoubleAnimation.cs" company="Microsoft">
//    Copyright (C) Microsoft Corporation.  All rights reserved.
// </copyright> 
//
// This file was generated, please do not edit it directly. 
// 
// Please see http://wiki/default.aspx/Microsoft.Projects.Avalon/MilCodeGen.html for more information.
// 
//---------------------------------------------------------------------------

using MS.Internal;
using MS.Internal.KnownBoxes; 
using MS.Utility;
 
using System; 
using System.Collections;
using System.ComponentModel; 
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Media; 
using System.Windows.Media.Media3D;
using System.Windows.Media.Animation; 
 
using MS.Internal.PresentationCore;
 
namespace System.Windows.Media.Animation
{

    /// <summary> 
    /// Animates the value of a Double property using linear interpolation
    /// between two values.  The values are determined by the combination of 
    /// From, To, or By values that are set on the animation. 
    /// </summary>
    public partial class DoubleAnimation : 
        DoubleAnimationBase
    {
        #region Data
 
        /// <summary>
        /// This is used if the user has specified From, To, and/or By values. 
        /// </summary> 
        private Double[] _keyValues;
 
        private AnimationType _animationType;
        private bool _isAnimationFunctionValid;

        #endregion 

        #region Constructors 
 
        /// <summary>
        /// Static ctor for DoubleAnimation establishes 
        /// dependency properties, using as much shared data as possible.
        /// </summary>
        static DoubleAnimation()
        { 
            Type typeofProp = typeof(Double?);
            Type typeofThis = typeof(DoubleAnimation); 
            PropertyChangedCallback propCallback = new PropertyChangedCallback(AnimationFunction_Changed); 
            ValidateValueCallback validateCallback = new ValidateValueCallback(ValidateFromToOrByValue);
 
            FromProperty = DependencyProperty.Register(
                "From",
                typeofProp,
                typeofThis, 
                new PropertyMetadata((Double?)null, propCallback),
                validateCallback); 
 
            ToProperty = DependencyProperty.Register(
                "To", 
                typeofProp,
                typeofThis,
                new PropertyMetadata((Double?)null, propCallback),
                validateCallback); 

            ByProperty = DependencyProperty.Register( 
                "By", 
                typeofProp,
                typeofThis, 
                new PropertyMetadata((Double?)null, propCallback),
                validateCallback);

            EasingFunctionProperty = DependencyProperty.Register( 
                "EasingFunction",
                typeof(IEasingFunction), 
                typeofThis); 
        }
 

        /// <summary>
        /// Creates a new DoubleAnimation with all properties set to
        /// their default values. 
        /// </summary>
        public DoubleAnimation() 
            : base() 
        {
        } 

        /// <summary>
        /// Creates a new DoubleAnimation that will animate a
        /// Double property from its base value to the value specified 
        /// by the "toValue" parameter of this constructor.
        /// </summary> 
        public DoubleAnimation(Double toValue, Duration duration) 
            : this()
        { 
            To = toValue;
            Duration = duration;
        }
 
        /// <summary>
        /// Creates a new DoubleAnimation that will animate a 
        /// Double property from its base value to the value specified 
        /// by the "toValue" parameter of this constructor.
        /// </summary> 
        public DoubleAnimation(Double toValue, Duration duration, FillBehavior fillBehavior)
            : this()
        {
            To = toValue; 
            Duration = duration;
            FillBehavior = fillBehavior; 
        } 

        /// <summary> 
        /// Creates a new DoubleAnimation that will animate a
        /// Double property from the "fromValue" parameter of this constructor
        /// to the "toValue" parameter.
        /// </summary> 
        public DoubleAnimation(Double fromValue, Double toValue, Duration duration)
            : this() 
        { 
            From = fromValue;
            To = toValue; 
            Duration = duration;
        }

        /// <summary> 
        /// Creates a new DoubleAnimation that will animate a
        /// Double property from the "fromValue" parameter of this constructor 
        /// to the "toValue" parameter. 
        /// </summary>
        public DoubleAnimation(Double fromValue, Double toValue, Duration duration, FillBehavior fillBehavior) 
            : this()
        {
            From = fromValue;
            To = toValue; 
            Duration = duration;
            FillBehavior = fillBehavior; 
        } 

        #endregion 

        #region Freezable

        /// <summary> 
        /// Creates a copy of this DoubleAnimation
        /// </summary> 
        /// <returns>The copy</returns> 
        public new DoubleAnimation Clone()
        { 
            return (DoubleAnimation)base.Clone();
        }

        // 
        // Note that we don't override the Clone virtuals (CloneCore, CloneCurrentValueCore,
        // GetAsFrozenCore, and GetCurrentValueAsFrozenCore) even though this class has state 
        // not stored in a DP. 
        //
        // We don't need to clone _animationType and _keyValues because they are the the cached 
        // results of animation function validation, which can be recomputed.  The other remaining
        // field, isAnimationFunctionValid, defaults to false, which causes this recomputation to happen.
        //
 
        /// <summary>
        /// Implementation of <see cref="System.Windows.Freezable.CreateInstanceCore">Freezable.CreateInstanceCore</see>. 
        /// </summary> 
        /// <returns>The new Freezable.</returns>
        protected override Freezable CreateInstanceCore() 
        {
            return new DoubleAnimation();
        }
 
        #endregion
 
        #region Methods 

        /// <summary> 
        /// Calculates the value this animation believes should be the current value for the property.
        /// </summary>
        /// <param name="defaultOriginValue">
        /// This value is the suggested origin value provided to the animation 
        /// to be used if the animation does not have its own concept of a
        /// start value. If this animation is the first in a composition chain 
        /// this value will be the snapshot value if one is available or the 
        /// base property value if it is not; otherise this value will be the
        /// value returned by the previous animation in the chain with an 
        /// animationClock that is not Stopped.
        /// </param>
        /// <param name="defaultDestinationValue">
        /// This value is the suggested destination value provided to the animation 
        /// to be used if the animation does not have its own concept of an
        /// end value. This value will be the base value if the animation is 
        /// in the first composition layer of animations on a property; 
        /// otherwise this value will be the output value from the previous
        /// composition layer of animations for the property. 
        /// </param>
        /// <param name="animationClock">
        /// This is the animationClock which can generate the CurrentTime or
        /// CurrentProgress value to be used by the animation to generate its 
        /// output value.
        /// </param> 
        /// <returns> 
        /// The value this animation believes should be the current value for the property.
        /// </returns> 
        protected override Double GetCurrentValueCore(Double defaultOriginValue, Double defaultDestinationValue, AnimationClock animationClock)
        {
            Debug.Assert(animationClock.CurrentState != ClockState.Stopped);
 
            if (!_isAnimationFunctionValid)
            { 
                ValidateAnimationFunction(); 
            }
 
            double progress = animationClock.CurrentProgress.Value;

            IEasingFunction easingFunction = EasingFunction;
            if (easingFunction != null) 
            {
                progress = easingFunction.Ease(progress); 
            } 

            Double   from        = new Double(); 
            Double   to          = new Double();
            Double   accumulated = new Double();
            Double   foundation  = new Double();
 
            // need to validate the default origin and destination values if
            // the animation uses them as the from, to, or foundation values 
            bool validateOrigin = false; 
            bool validateDestination = false;
 
            switch(_animationType)
            {
                case AnimationType.Automatic:
 
                    from    = defaultOriginValue;
                    to      = defaultDestinationValue; 
 
                    validateOrigin = true;
                    validateDestination = true; 

                    break;

                case AnimationType.From: 

                    from    = _keyValues[0]; 
                    to      = defaultDestinationValue; 

                    validateDestination = true; 

                    break;

                case AnimationType.To: 

                    from = defaultOriginValue; 
                    to = _keyValues[0]; 

                    validateOrigin = true; 

                    break;

                case AnimationType.By: 

                    // According to the SMIL specification, a By animation is 
                    // always additive.  But we don't force this so that a 
                    // user can re-use a By animation and have it replace the
                    // animations that precede it in the list without having 
                    // to manually set the From value to the base value.

                    to          = _keyValues[0];
                    foundation  = defaultOriginValue; 

                    validateOrigin = true; 
 
                    break;
 
                case AnimationType.FromTo:

                    from    = _keyValues[0];
                    to      = _keyValues[1]; 

                    if (IsAdditive) 
                    { 
                        foundation = defaultOriginValue;
                        validateOrigin = true; 
                    }

                    break;
 
                case AnimationType.FromBy:
 
                    from    = _keyValues[0]; 
                    to      = AnimatedTypeHelpers.AddDouble(_keyValues[0], _keyValues[1]);
 
                    if (IsAdditive)
                    {
                        foundation = defaultOriginValue;
                        validateOrigin = true; 
                    }
 
                    break; 

                default: 

                    Debug.Fail("Unknown animation type.");

                    break; 
            }
 
            if (validateOrigin 
                && !AnimatedTypeHelpers.IsValidAnimationValueDouble(defaultOriginValue))
            { 
                throw new InvalidOperationException(
                    SR.Get(
                        SRID.Animation_Invalid_DefaultValue,
                        this.GetType(), 
                        "origin",
                        defaultOriginValue.ToString(CultureInfo.InvariantCulture))); 
            } 

            if (validateDestination 
                && !AnimatedTypeHelpers.IsValidAnimationValueDouble(defaultDestinationValue))
            {
                throw new InvalidOperationException(
                    SR.Get( 
                        SRID.Animation_Invalid_DefaultValue,
                        this.GetType(), 
                        "destination", 
                        defaultDestinationValue.ToString(CultureInfo.InvariantCulture)));
            } 


            if (IsCumulative)
            { 
                double currentRepeat = (double)(animationClock.CurrentIteration - 1);
 
                if (currentRepeat > 0.0) 
                {
                    Double accumulator = AnimatedTypeHelpers.SubtractDouble(to, from); 

                    accumulated = AnimatedTypeHelpers.ScaleDouble(accumulator, currentRepeat);
                }
            } 

            // return foundation + accumulated + from + ((to - from) * progress) 
 
            return AnimatedTypeHelpers.AddDouble(
                foundation, 
                AnimatedTypeHelpers.AddDouble(
                    accumulated,
                    AnimatedTypeHelpers.InterpolateDouble(from, to, progress)));
        } 

        private void ValidateAnimationFunction() 
        { 
            _animationType = AnimationType.Automatic;
            _keyValues = null; 

            if (From.HasValue)
            {
                if (To.HasValue) 
                {
                    _animationType = AnimationType.FromTo; 
                    _keyValues = new Double[2]; 
                    _keyValues[0] = From.Value;
                    _keyValues[1] = To.Value; 
                }
                else if (By.HasValue)
                {
                    _animationType = AnimationType.FromBy; 
                    _keyValues = new Double[2];
                    _keyValues[0] = From.Value; 
                    _keyValues[1] = By.Value; 
                }
                else 
                {
                    _animationType = AnimationType.From;
                    _keyValues = new Double[1];
                    _keyValues[0] = From.Value; 
                }
            } 
            else if (To.HasValue) 
            {
                _animationType = AnimationType.To; 
                _keyValues = new Double[1];
                _keyValues[0] = To.Value;
            }
            else if (By.HasValue) 
            {
                _animationType = AnimationType.By; 
                _keyValues = new Double[1]; 
                _keyValues[0] = By.Value;
            } 

            _isAnimationFunctionValid = true;
        }
 
        #endregion
 
        #region Properties 

        private static void AnimationFunction_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) 
        {
            DoubleAnimation a = (DoubleAnimation)d;

            a._isAnimationFunctionValid = false; 
            a.PropertyChanged(e.Property);
        } 
 
        private static bool ValidateFromToOrByValue(object value)
        { 
            Double? typedValue = (Double?)value;

            if (typedValue.HasValue)
            { 
                return AnimatedTypeHelpers.IsValidAnimationValueDouble(typedValue.Value);
            } 
            else 
            {
                return true; 
            }
        }

        /// <summary> 
        /// FromProperty
        /// </summary> 
        public static readonly DependencyProperty FromProperty; 

        /// <summary> 
        /// From
        /// </summary>
        public Double? From
        { 
            get
            { 
                return (Double?)GetValue(FromProperty); 
            }
            set 
            {
                SetValueInternal(FromProperty, value);
            }
        } 

        /// <summary> 
        /// ToProperty 
        /// </summary>
        public static readonly DependencyProperty ToProperty; 

        /// <summary>
        /// To
        /// </summary> 
        public Double? To
        { 
            get 
            {
                return (Double?)GetValue(ToProperty); 
            }
            set
            {
                SetValueInternal(ToProperty, value); 
            }
        } 
 
        /// <summary>
        /// ByProperty 
        /// </summary>
        public static readonly DependencyProperty ByProperty;

        /// <summary> 
        /// By
        /// </summary> 
        public Double? By 
        {
            get 
            {
                return (Double?)GetValue(ByProperty);
            }
            set 
            {
                SetValueInternal(ByProperty, value); 
            } 
        }
 

        /// <summary>
        /// EasingFunctionProperty
        /// </summary> 
        public static readonly DependencyProperty EasingFunctionProperty;
 
        /// <summary> 
        /// EasingFunction
        /// </summary> 
        public IEasingFunction EasingFunction
        {
            get
            { 
                return (IEasingFunction)GetValue(EasingFunctionProperty);
            } 
            set 
            {
                SetValueInternal(EasingFunctionProperty, value); 
            }
        }

        /// <summary> 
        /// If this property is set to true the animation will add its value to
        /// the base value instead of replacing it entirely. 
        /// </summary> 
        public bool IsAdditive
        { 
            get
            {
                return (bool)GetValue(IsAdditiveProperty);
            } 
            set
            { 
                SetValueInternal(IsAdditiveProperty, BooleanBoxes.Box(value)); 
            }
        } 

        /// <summary>
        /// It this property is set to true, the animation will accumulate its
        /// value over repeats.  For instance if you have a From value of 0.0 and 
        /// a To value of 1.0, the animation return values from 1.0 to 2.0 over
        /// the second reteat cycle, and 2.0 to 3.0 over the third, etc. 
        /// </summary> 
        public bool IsCumulative
        { 
            get
            {
                return (bool)GetValue(IsCumulativeProperty);
            } 
            set
            { 
                SetValueInternal(IsCumulativeProperty, BooleanBoxes.Box(value)); 
            }
        } 

        #endregion
    }
} 

