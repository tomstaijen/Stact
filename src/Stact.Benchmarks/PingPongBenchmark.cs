// Copyright 2010 Chris Patterson
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
namespace Stact.Benchmarks
{
	using System;
	using System.Diagnostics;
	using Magnum.Concurrency;
	using Magnum.Extensions;


	public class PingPongBenchmark
	{
		public void Run()
		{
			var serverFactory = ActorFactory.Create(fiber => new PingServer(fiber));
			var server = serverFactory.GetActor();
			var server2 = serverFactory.GetActor();

			ActorInstance[] servers = new[] {server, server2};


			Stopwatch timer = Stopwatch.StartNew();

			const int actorCount = 10;
			const int pingCount = 20000;

			var actors = new ActorInstance[actorCount];

			var complete = new Future<int>();

			var latch = new CountdownLatch(actorCount * pingCount, complete.Complete);

			for (int i = 0; i < actorCount; i++)
			{
				var s = servers[i%2];
				actors[i] = AnonymousActor.New(inbox =>
					{
						var ping = new Ping();
						int count = 0;
						Action loop = null;
						loop = () =>
							{
								s.Request(ping, inbox)
									.Receive<Response<Pong>>(response =>
										{
											latch.CountDown();
											count++;
											if (count < pingCount)
												loop();
										});
							};

						loop();
					});
			}

			bool completed = complete.WaitUntilCompleted(5.Minutes());

			timer.Stop();

			for (int i = 0; i < actorCount; i++)
			{
				actors[i].Exit();
				actors[i] = null;
			}

			server.Exit();

			if (!completed)
			{
				Console.WriteLine("Process did not complete");
				return;
			}

			Console.WriteLine("Processed {0} messages in with {1} channels in {2}ms", pingCount, actorCount, timer.ElapsedMilliseconds);

			Console.WriteLine("That's {0} messages per second!", ((long)pingCount * actorCount * 1000) / timer.ElapsedMilliseconds);
		}


		class PingServer :
			Actor
		{
			Pong _response;

			public PingServer(Fiber fiber)
			{
				_response = new Pong();

				this.Connect(x => x.PingChannel, fiber, HandlePing);
			}

			public Channel<Request<Ping>> PingChannel { get; private set; }

			void HandlePing(Request<Ping> request)
			{
				request.Respond(_response);
			}
		}

		class Ping
		{
		}

		class Pong
		{
			
		}
	}
}