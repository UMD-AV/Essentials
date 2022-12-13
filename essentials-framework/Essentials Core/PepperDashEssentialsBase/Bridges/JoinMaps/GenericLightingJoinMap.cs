﻿using System;


namespace PepperDash.Essentials.Core.Bridges
{
    public class GenericLightingJoinMap : JoinMapBaseAdvanced
    {
        [JoinName("IsOnline")]
        public JoinDataComplete IsOnline = new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata { Description = "Lighting Controller Online", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Digital });

        [JoinName("SelectButton")]
        public JoinDataComplete SelectButton = new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata { Description = "Lighting Controller Select Button By Index", JoinCapabilities = eJoinCapabilities.FromSIMPL, JoinType = eJoinType.Analog });

        [JoinName("SelectButtonDirect")]
        public JoinDataComplete SelectButtonDirect = new JoinDataComplete(new JoinData { JoinNumber = 11, JoinSpan = 10 },
            new JoinMetadata { Description = "Lighting Controller Select Button and Feedback", JoinCapabilities = eJoinCapabilities.ToFromSIMPL, JoinType = eJoinType.DigitalSerial });

        [JoinName("ButtonVisibility")]
        public JoinDataComplete ButtonVisibility = new JoinDataComplete(new JoinData { JoinNumber = 21, JoinSpan = 10 },
            new JoinMetadata { Description = "Lighting Controller Button Visibility", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Digital });

        [JoinName("IntegrationIdSet")]
        public JoinDataComplete IntegrationIdSet = new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata { Description = "Lighting Controller Set Integration Id", JoinCapabilities = eJoinCapabilities.FromSIMPL, JoinType = eJoinType.Serial });

        [JoinName("Name")]
        public JoinDataComplete Name = new JoinDataComplete(new JoinData { JoinNumber = 1, JoinSpan = 1 },
            new JoinMetadata { Description = "Lighting Controller Name", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });

        [JoinName("ErrorFb")]
        public JoinDataComplete ErrorFb = new JoinDataComplete(new JoinData { JoinNumber = 10, JoinSpan = 1 },
            new JoinMetadata { Description = "Lighting Controller Error Fb", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });

        [JoinName("ButtonTextFb")]
        public JoinDataComplete ButtonTextFb = new JoinDataComplete(new JoinData { JoinNumber = 11, JoinSpan = 10 },
            new JoinMetadata { Description = "Lighting Controller Button Text Fb", JoinCapabilities = eJoinCapabilities.ToSIMPL, JoinType = eJoinType.Serial });


        /// <summary>
        /// Constructor to use when instantiating this Join Map without inheriting from it
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        public GenericLightingJoinMap(uint joinStart)
            : this(joinStart, typeof(GenericLightingJoinMap))
        {
        }

        /// <summary>
        /// Constructor to use when extending this Join map
        /// </summary>
        /// <param name="joinStart">Join this join map will start at</param>
        /// <param name="type">Type of the child join map</param>
        protected GenericLightingJoinMap(uint joinStart, Type type) : base(joinStart, type)
        {
        }
    }
}