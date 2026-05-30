@pushd src
python Program.py /b2b:..\..\SchemaManipulator\Data\WorldData.bin,..\Data\WorldData.bin
python Program.py /b2j:..\Data\WorldData.bin,..\Data\WorldData.json
python Program.py /j2b:..\Data\WorldData.json,..\Data\WorldData.json.bin
@popd
