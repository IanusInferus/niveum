package clients 
{
    import flash.errors.IllegalOperationError;
    import flash.utils.ByteArray;
    import communication.*;
    public class BufferStateMachine 
    {
        private var state:int;
        // 0 初始状态
        // 1 已读取NameLength
        // 2 已读取CommandHash
        // 3 已读取Name
        // 4 已读取ParametersLength

        private var commandNameLength:int;
        private var commandName:String;
        private var commandHash:uint;
        private var parametersLength:int;

        public function BufferStateMachine()
        {
            state = 0;
        }

        public function TryShift(buffer:ByteArray, position:int, length:int):TryShiftResult
        {
            buffer.position = position;
            if (state == 0)
            {
                if (length >= 4)
                {
                    commandNameLength = BinaryTranslator.int32FromBinary(buffer);
                    if (commandNameLength < 0 || commandNameLength > 128) { throw new IllegalOperationError(); }
                    var r:TryShiftResult = new TryShiftResult();
                    r.command = null;
                    r.position = position + 4;
                    state = 1;
                    return r;
                }
                return null;
            }
            else if (state == 1)
            {
                if (length >= commandNameLength)
                {
                    var commandNameBytes:ByteArray = new ByteArray();
                    commandNameBytes.length = commandNameLength + 4;
                    BinaryTranslator.int32ToBinary(commandNameBytes, commandNameLength);
                    commandNameBytes.position = 4;
                    buffer.readBytes(commandNameBytes, 4, commandNameLength);
                    commandNameBytes.position = 0;
                    commandName = BinaryTranslator.stringFromBinary(commandNameBytes);
                    var r:TryShiftResult = new TryShiftResult();
                    r.command = null;
                    r.position = position + commandNameLength;
                    state = 2;
                    return r;
                }
                return null;
            }
            else if (state == 2)
            {
                if (length >= commandHash)
                {
                    commandHash = BinaryTranslator.uint32FromBinary(buffer);
                    var r:TryShiftResult = new TryShiftResult();
                    r.command = null;
                    r.position = position + 4;
                    state = 3;
                    return r;
                }
                return null;
            }
            if (state == 3)
            {
                if (length >= 4)
                {
                    parametersLength = BinaryTranslator.int32FromBinary(buffer);
                    if (parametersLength < 0 || parametersLength > 8 * 1024) { throw new IllegalOperationError(); }
                    var r:TryShiftResult = new TryShiftResult();
                    r.command = null;
                    r.position = position + 4;
                    state = 4;
                    return r;
                }
                return null;
            }
            else if (state == 4)
            {
                if (length >= parametersLength)
                {
                    var parameters:ByteArray = new ByteArray();
                    parameters.length = parametersLength;
                    buffer.readBytes(parameters, 0, parametersLength);
                    parameters.position = 0;
                    var cmd:Command = new Command();
                    cmd.commandName = commandName;
                    cmd.commandHash = commandHash;
                    cmd.parameters = parameters;
                    var r:TryShiftResult = new TryShiftResult();
                    r.command = cmd;
                    r.position = position + parametersLength;
                    commandNameLength = 0;
                    commandName = null;
                    commandHash = 0;
                    parametersLength = 0;
                    state = 0;
                    return r;
                }
                return null;
            }
            else
            {
                throw new IllegalOperationError();
            }
        }
    }
}