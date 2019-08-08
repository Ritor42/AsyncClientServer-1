using System;
using System.Linq;
using System.Threading;
using AsyncClientServer.Client;
using AsyncClientServer.Messaging.Metadata;
using AsyncClientServer.Server;

namespace AsyncClientServer.Messaging.Handlers
{
    internal class MessageHandlerState : SocketStateState
    {

        public MessageHandlerState(ISocketState state, SocketClient client, ServerListener listener) : base(state, client, listener)
        {
        }


        private void Write(int receive)
        {
            var bytes = new byte[receive];
            State.AppendRead(receive);

            if (State.Read > State.MessageSize)
            {
                int extraRead = State.Read - State.MessageSize;

                var extraBytes = new byte[extraRead];
                Array.Copy(State.Buffer, receive - extraRead, extraBytes, 0, extraRead);

                bytes = new byte[receive - extraRead];
                Array.Copy(State.Buffer, 0, bytes, 0, receive - extraRead);

                State.SubtractRead(extraRead);
                State.ChangeBuffer(extraBytes);
                State.Flag = -3;
            }
            else if (State.Read == State.MessageSize)
            {
                State.Flag = -2;
                Array.Copy(State.Buffer, 0, bytes, 0, bytes.Length);
            }
            else if(receive == State.BufferSize)
            {
                bytes = State.Buffer;
            }
            else
            {
                bytes = State.Buffer.Take(receive).ToArray();
            }

            State.AppendBytes(bytes);
        }

        /// <summary>
        /// Writes message data to state stringBuilder
        /// </summary>
        /// <param name="receive"></param>
        public override void Receive(int receive)
        {
            //Write the message to stringBuilder
            Write(receive);

            //Check if message has been received with no extra bytes
            if (State.Flag == -2)
            {
                //Change state to new MessageHasBeenReceivedState invoke the corresponding event, then reset the state object.
                State.CurrentState = new MessageHasBeenReceivedState(State, Client, Server);
                State.CurrentState.Receive(State.MessageSize);
                State.Reset();
            }
            //Handle the extra bytes.
            else if (State.Flag == -3)
            {
                //Change state to MessageHasBeenReceivedState and invoke the corresponding event.
                State.CurrentState = new MessageHasBeenReceivedState(State, Client, Server);
                State.CurrentState.Receive(State.MessageSize);

                //Change state to InitialHandlerState, reset the state and then handle the extra bytes that have been send.
                State.CurrentState = new InitialHandlerState(State, Client, Server);
                State.Reset();
                State.CurrentState.Receive(State.Buffer.Length);
            }
        }
    }
}
