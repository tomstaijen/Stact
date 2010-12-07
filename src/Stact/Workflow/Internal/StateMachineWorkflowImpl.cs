﻿// Copyright 2010 Chris Patterson
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace Stact.Workflow.Internal
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Linq.Expressions;
	using Magnum;
	using Magnum.Extensions;


	public class StateMachineWorkflowImpl<TWorkflow, TInstance> :
		StateMachineWorkflow<TWorkflow, TInstance>
		where TWorkflow : class
		where TInstance : class
	{
		readonly State<TInstance> _anyState;
		readonly StateAccessor<TInstance> _currentStateAccessor;
		readonly IDictionary<string, EventRaiser<TInstance>> _events;
		readonly IDictionary<string, State<TInstance>> _states;

		public StateMachineWorkflowImpl(StateAccessor<TInstance> currentStateAccessor,
		                                IDictionary<string, State<TInstance>> states,
		                                IEnumerable<Event> events,
		                                State<TInstance> anyState)
		{
			_currentStateAccessor = currentStateAccessor;
			_anyState = anyState;
			_states = states;
			_events = events.Select(x => new EventRaiser<TInstance>(x)).ToDictionary(x => x.Event.Name);
		}

		public void Accept(StateMachineVisitor visitor)
		{
			visitor.Visit(this);

			_states.Values.Each(x => x.Accept(visitor));
			_events.Values.Each(x => x.Event.Accept(visitor));
		}

		public void RaiseEvent(TInstance instance, string eventName)
		{
			WithEvent(eventName, e =>
				{
					State<TInstance> state = _currentStateAccessor.Get(instance);

					e.RaiseEvent(state, instance);
				});
		}

		public void RaiseEvent(TInstance instance, string eventName, object body)
		{
			WithEvent(eventName, e =>
				{
					State<TInstance> state = _currentStateAccessor.Get(instance);

					e.RaiseEvent(state, instance, body);
					e.RaiseEvent(_anyState, instance, body);
				});
		}

		public void RaiseEvent(TInstance instance, Expression<Func<TWorkflow, Event>> eventSelector)
		{
			string eventName = eventSelector.GetEventName();
			WithEvent(eventName, e =>
				{
					State<TInstance> state = _currentStateAccessor.Get(instance);

					e.RaiseEvent(state, instance);
					e.RaiseEvent(_anyState, instance);
				});
		}

		public void RaiseEvent<TBody>(TInstance instance, Expression<Func<TWorkflow, Event<TBody>>> eventSelector, TBody body)
		{
			string eventName = eventSelector.GetEventName();
			WithEvent(eventName, e =>
				{
					State<TInstance> state = _currentStateAccessor.Get(instance);

					e.RaiseEvent(state, instance, body);
					e.RaiseEvent(_anyState, instance, body);
				});
		}

		public State GetCurrentState(TInstance instance)
		{
			Guard.AgainstNull(instance, "instance");

			return _currentStateAccessor.Get(instance);
		}

		void WithEvent(string eventName, Action<EventRaiser<TInstance>> callback)
		{
			EventRaiser<TInstance> e;
			if (_events.TryGetValue(eventName, out e))
				callback(e);
			else
			{
				throw new StateMachineWorkflowException("Unknown event: {0}.{1}"
				                                        	.FormatWith(typeof(TWorkflow).Name, eventName));
			}
		}
	}
}