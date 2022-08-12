// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class FlashlightEvaluator
    {
        private const double max_opacity_bonus = 0.4;
        private const double hidden_bonus = 0.2;

        private const double min_velocity = 0.5;
        private const double slider_multiplier = 1.3;

        private const double min_grid_multiplier = 0.35;

        /// <summary>
        /// Evaluates the difficulty of memorising and hitting an object, based on:
        /// <list type="bullet">
        /// <item><description>distance between a number of previous objects and the current object,</description></item>
        /// <item><description>the visual opacity of the current object,</description></item>
        /// <item><description>the angle made by the current object,</description></item>
        /// <item><description>length and speed of the current object (for sliders),</description></item>
        /// <item><description>and whether the hidden mod is enabled.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool hidden)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrent = (OsuDifficultyHitObject)current;
            var osuHitObject = (OsuHitObject)(osuCurrent.BaseObject);

            double scalingFactor = 52.0 / osuHitObject.Radius;
            double smallDistNerf = 1.0;
            double cumulativeStrainTime = 0.0;

            double result = 0.0;

            OsuDifficultyHitObject lastObj = osuCurrent;

            // This is iterating backwards in time from the current object.
            for (int i = 0; i < Math.Min(current.Index, 10); i++)
            {
                var currentObj = (OsuDifficultyHitObject)current.Previous(i);
                var currentHitObject = (OsuHitObject)(currentObj.BaseObject);

                if (!(currentObj.BaseObject is Spinner))
                {
                    double jumpDistance = (osuHitObject.StackedPosition - currentHitObject.EndPosition).Length;

                    cumulativeStrainTime += lastObj.StrainTime;

                    // We want to nerf objects that can be easily seen within the Flashlight circle radius.
                    if (i == 0)
                        smallDistNerf = Math.Min(1.0, jumpDistance / 75.0);

                    // We also want to nerf stacks so that only the first object of the stack is accounted for.
                    double stackNerf = Math.Min(1.0, (currentObj.LazyJumpDistance / scalingFactor) / 25.0);

                    // Bonus based on how visible the object is.
                    double opacityBonus = 1.0 + max_opacity_bonus * (1.0 - osuCurrent.OpacityAt(currentHitObject.StartTime, hidden));

                    result += stackNerf * opacityBonus * scalingFactor * jumpDistance / cumulativeStrainTime;
                }

                lastObj = currentObj;
            }

            result = Math.Pow(smallDistNerf * result, 2.0);

            // Additional bonus for Hidden due to there being no approach circles.
            if (hidden)
                result *= 1.0 + hidden_bonus;

            // Nerf patterns with angles that are commonly used in grid maps.
            // 0 deg, 60 deg, 120 deg and 180 deg are commonly used in hexgrid maps.
            // 0 deg, 45 deg, 90 deg, 135 deg and 180 deg are commonly used in squaregrid maps.
            if (osuCurrent.Angle != null)
            {
                double hexgridMultiplier = 1.0 - Math.Pow(Math.Cos((180 / 60.0) * (double)(osuCurrent.Angle)), 20.0);
                double squaregridMultiplier = 1.0 - Math.Pow(Math.Cos((180 / 45.0) * (double)(osuCurrent.Angle)), 20.0);
                result *= (1.0 - min_grid_multiplier) * hexgridMultiplier * squaregridMultiplier + min_grid_multiplier;
            }

            double sliderBonus = 0.0;

            if (osuCurrent.BaseObject is Slider osuSlider)
            {
                // Invert the scaling factor to determine the true travel distance independent of circle size.
                double pixelTravelDistance = osuSlider.LazyTravelDistance / scalingFactor;

                // Reward sliders based on velocity.
                sliderBonus = Math.Pow(Math.Max(0.0, pixelTravelDistance / osuCurrent.TravelTime - min_velocity), 0.5);

                // Longer sliders require more memorisation.
                sliderBonus *= pixelTravelDistance;

                // Nerf sliders with repeats, as less memorisation is required.
                if (osuSlider.RepeatCount > 0)
                    sliderBonus /= (osuSlider.RepeatCount + 1);
            }

            result += sliderBonus * slider_multiplier;

            return result;
        }
    }
}
