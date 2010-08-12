﻿// Copyright 2007-2010 The Apache Software Foundation.
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
namespace Magnum.Specs.Servers
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Net;
	using System.Net.Sockets;
	using System.Text;
	using System.Threading;
	using Fibers;
	using Magnum.Channels;
	using Magnum.Extensions;
	using Magnum.Logging;
	using Magnum.Servers;
	using Magnum.Servers.Events;
	using NUnit.Framework;
	using TestFramework;


	[Scenario, Explicit]
	public class When_starting_an_http_server
	{
		ChannelConnection _connection;
		ChannelAdapter _input;
		Future<ServerRunning> _runningEventReceived;
		protected HttpServer _server;
		Future<ServerStarting> _startingEventReceived;

		public Uri ServerUri { get; private set; }

		[When]
		public void Starting_a_socket_server()
		{
			TraceLogProvider.Configure(LogLevel.Debug);

			_startingEventReceived = new Future<ServerStarting>();
			_runningEventReceived = new Future<ServerRunning>();

			_input = new ChannelAdapter();
			_connection = _input.Connect(x =>
				{
					x.AddConsumerOf<ServerStarting>()
						.UsingConsumer(_startingEventReceived.Complete)
						.ExecuteOnProducerThread();

					x.AddConsumerOf<ServerRunning>()
						.UsingConsumer(_runningEventReceived.Complete)
						.ExecuteOnProducerThread();
				});

			ServerUri = new Uri("http://localhost:8008/");
			_server = new HttpServer(ServerUri, new ThreadPoolFiber(), _input);
			_server.Start();
		}

		[After]
		public void Finally()
		{
			_server.Stop();

			_connection.Dispose();
		}

		[Then]
		public void Should_be_available_to_accept_connections()
		{
			_server.CurrentState.ShouldEqual(HttpServer.Running);
		}

		[Then]
		public void Should_have_called_the_starting_event()
		{
			AssertionsForBoolean.ShouldBeTrue(_startingEventReceived.WaitUntilCompleted(2.Seconds()));
		}

		[Then]
		public void Should_have_called_the_running_event()
		{
			AssertionsForBoolean.ShouldBeTrue(_runningEventReceived.WaitUntilCompleted(2.Seconds()));
		}
	}

	[Scenario, Explicit]
	public class When_connecting_to_an_http_server :
		When_starting_an_http_server
	{
		HttpWebRequest _webRequest;
		WebResponse _webResponse;

		[When]
		public void Connecting_to_a_socket_server()
		{
			_webRequest = (HttpWebRequest)WebRequest.Create(ServerUri);
			_webResponse = _webRequest.GetResponse();
		}

		[After]
		public void My_Finally()
		{
			using (_webResponse)
			{
				_webResponse.Close();
			}
		}

		[Then]
		public void Should_establish_a_connection()
		{
			//_webResponse.Connected.ShouldBeTrue();
		}

		[Then]
		public void Should_allow_multiple_connections()
		{
			var requests = new List<HttpWebRequest>();

			Stopwatch connectionTimer = Stopwatch.StartNew();

			int expected = 100;
			for (int i = 0; i < expected; i++)
			{
				var webRequest = (HttpWebRequest)WebRequest.Create(ServerUri);
				webRequest.Method = "PUT";
				using (var reque = webRequest.GetRequestStream())
				{
					byte[] buffer = Encoding.UTF8.GetBytes("Hello");

					reque.Write(buffer, 0, buffer.Length);
				}

				requests.Add(webRequest);
			}

			connectionTimer.Stop();

			Trace.WriteLine("Established {0} connections in {0}ms".FormatWith(expected, connectionTimer.ElapsedMilliseconds));

			requests.ForEach(request =>
			{
				using (WebResponse webResponse = request.GetResponse())
				{
					webResponse.Close();
				}
			});

			requests.Clear();
		}
	}
}