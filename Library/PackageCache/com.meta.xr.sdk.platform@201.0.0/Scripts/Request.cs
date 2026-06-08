using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnityEngine;

namespace Oculus.Platform
{
    /// This Request class provides a set of tools and services for developing VR applications.
    /// It represents a request made to the Oculus Platform, such as a request to initialize the platform or to retrieve user data.
    public sealed class Request<T> : Request
    {
        private TaskCompletionSource<Message<T>> tcs_ = null;
        private Message<T>.Callback callback_ = null;

        public Request(ulong requestID) : base(requestID)
        {
        }
        /// This function takes a callback function as a parameter and attaches it to the request. When the request is completed, the callback function will be called with the result of the request.
        public Request<T> OnComplete(Message<T>.Callback callback)
        {
            if (callback_ != null)
            {
                throw new UnityException("Attempted to attach multiple handlers to a Request.  This is not allowed.");
            }

            if (tcs_ != null)
            {
                throw new UnityException("Attempted to attach multiple handlers to a Request.  This is not allowed.");
            }

            callback_ = callback;
            Callback.AddRequest(this);
            return this;
        }

        new public async Task<Message<T>> Gen()
        {
            if (callback_ != null || tcs_ != null)
            {
                throw new UnityException("Attempted to attach multiple handlers to a Request.  This is not allowed.");
            }

            tcs_ = new TaskCompletionSource<Message<T>>();
            Callback.AddRequest(this);
            return await tcs_.Task;
        }

        /// Makes the Request<T> class awaitable, allowing it to be used with the await keyword.
        /// Returns an awaiter that completes when the request completes.
        new public TaskAwaiter<Message<T>> GetAwaiter()
        {
            return Gen().GetAwaiter();
        }

        ///  This function is called when a message is received from the Oculus Platform in response to the request.
        /// It takes a Message object as a parameter, which contains the result of the request.
        override public void HandleMessage(Message msg)
        {
            if (!(msg is Message<T>))
            {
                Debug.LogError("Unable to handle message: " + msg.GetType());
                return;
            }

            if (tcs_ != null)
            {
                tcs_.SetResult((Message<T>)msg);
                return;
            }

            if (callback_ != null)
            {
                callback_((Message<T>)msg);
                return;
            }

            throw new UnityException("Request with no handler.  This should never happen.");
        }
    }

    public class Request
    {
        private TaskCompletionSource<Message> tcs_;
        private Message.Callback callback_;

        public Request(ulong requestID)
        {
            this.RequestID = requestID;
        }
        ///  It is a public property of the Request class that represents the unique identifier for a request. It can be used to identify and track requests.
        public ulong RequestID { get; set; }

        public Request OnComplete(Message.Callback callback)
        {
            callback_ = callback;
            Callback.AddRequest(this);
            return this;
        }

        public async Task<Message> Gen()
        {
            tcs_ = new TaskCompletionSource<Message>();
            Callback.AddRequest(this);
            return await tcs_.Task;
        }

        /// Makes the Request class awaitable, allowing it to be used with the await keyword.
        /// Returns an awaiter that completes when the request completes.</returns>
        public TaskAwaiter<Message> GetAwaiter()
        {
            return Gen().GetAwaiter();
        }

        /// It is called when a message is received in response to a request made by the application.
        virtual public void HandleMessage(Message msg)
        {
            if (tcs_ != null)
            {
                tcs_.SetResult(msg);
                return;
            }

            if (callback_ != null)
            {
                callback_(msg);
                return;
            }

            throw new UnityException("Request with no handler.  This should never happen.");
        }

        /**
     * This will run callbacks on all messages that returned from the server.
     * If too many message are coming back at once, then a limit can be passed in
     * as an arg to limit the number of messages to run callbacks on at a time
     */
        public static void RunCallbacks(uint limit = 0)
        {
            // default of 0 will run callbacks on all messages on the queue
            if (limit == 0)
            {
                Callback.RunCallbacks();
            }
            else
            {
                Callback.RunLimitedCallbacks(limit);
            }
        }
    }
}
