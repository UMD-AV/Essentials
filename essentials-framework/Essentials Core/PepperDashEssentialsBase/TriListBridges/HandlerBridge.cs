﻿namespace PepperDash.Essentials.Core
{
	public abstract class HandlerBridge
	{
		public bool IsAttached { get; protected set; }

		/// <summary>
		/// Attaches the handler to the panel's user objects
		/// </summary>
		public abstract void AttachToTriListOutputs(bool sendUpdate);

		/// <summary>
		/// Removes the handler from the panel's user objects
		/// </summary>
		public abstract void DetachFromTriListOutputs();
	}
}