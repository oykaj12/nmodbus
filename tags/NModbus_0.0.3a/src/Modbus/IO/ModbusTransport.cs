using System;
using System.Collections.Generic;
using System.Text;
using log4net;
using Modbus.Message;
using Modbus.Util;
using System.IO;

namespace Modbus.IO
{
	public abstract class ModbusTransport
	{
		private static readonly ILog _log = LogManager.GetLogger(typeof(ModbusTransport));
		private int _retries = Modbus.DefaultRetries;

		public ModbusTransport()
		{
		}

		public int Retries
		{
			get { return _retries; }
			set { _retries = value; }
		}

		internal virtual T UnicastMessage<T>(IModbusMessage message) where T : IModbusMessage, new()
		{
			T response = default(T);
			int attempt = 1;
			bool success = false;

			do
			{
				try
				{
					// write message
					_log.InfoFormat("TX: {0}", StringUtil.Join(", ", message.MessageFrame));
					Write(message);

					// read response
					response = CreateResponse<T>(ReadResponse());
					_log.InfoFormat("RX: {0}", StringUtil.Join(", ", response.MessageFrame));

					// ensure response is of appropriate function code
					if (message.FunctionCode != response.FunctionCode)
					{
						string errorMessage = String.Format(ModbusResources.InvalidResponseExceptionMessage, message.FunctionCode, response.FunctionCode);
						_log.ErrorFormat(errorMessage);
						throw new IOException(errorMessage);
					}

					success = true;
				}
				catch (TimeoutException te)
				{
					_log.ErrorFormat(te.Message);
					throw te;
				}
				catch (Exception e)
				{
					_log.ErrorFormat("Failure {0}, {1} retries remaining. {2}", attempt, _retries + 1 - attempt, e.Message);

					if (attempt++ > _retries)
						throw e;
				}				
			} while (!success);

			return response;
		}

		internal virtual T CreateResponse<T>(byte[] frame) where T : IModbusMessage, new()
		{
			byte functionCode = frame[1];

			// check for slave exception response
			if (functionCode > Modbus.ExceptionOffset)
				throw new SlaveException(ModbusMessageFactory.CreateModbusMessage<SlaveExceptionResponse>(frame));

			// create message from frame
			T response = ModbusMessageFactory.CreateModbusMessage<T>(frame);

			return response;
		}

		internal abstract byte[] ReadRequest();
		internal abstract byte[] ReadResponse();
		internal abstract byte[] BuildMessageFrame(IModbusMessage message);		
		internal abstract void Write(IModbusMessage message);
	}
}
