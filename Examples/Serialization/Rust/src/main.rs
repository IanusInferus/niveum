//==========================================================================
//
//  File:        main.rs
//  Location:    Niveum.Examples <Rust>
//  Description: 数据复制工具
//  Version:     2026.06.07.
//  Author:      F.R.C.
//  Copyright(C) Public Domain
//
//==========================================================================

#[path = "generated/world.rs"]
mod world_types;

#[path = "generated/world_binary.rs"]
mod world_binary;

use std::env;
use std::fs;
use std::process;

use world_binary::BinaryTranslator;
use world_binary::ByteArrayStream;
use world_binary::WritableStream;

fn display_info(prog_name: &str) {
    eprintln!("数据复制工具");
    eprintln!("DataCopy，Public Domain");
    eprintln!("F.R.C.");
    eprintln!();
    eprintln!("用法:");
    eprintln!("{} <BinaryFile1> <BinaryFile2>", prog_name);
    eprintln!("复制二进制数据并以二进制形式写回，验证序列化完整性");
    eprintln!();
    eprintln!("BinaryFile1 二进制文件路径。");
    eprintln!("BinaryFile2 二进制文件路径。");
    eprintln!();
    eprintln!("示例:");
    eprintln!("{} ..\\..\\SchemaManipulator\\Data\\WorldData.bin ..\\Data\\WorldData.bin", prog_name);
    eprintln!("复制WorldData.bin。");
}

fn binary_to_binary(input_path: &str, output_path: &str) -> Result<(), Box<dyn std::error::Error>> {
    // Read input file
    let data = fs::read(input_path)
        .map_err(|e| format!("Failed to read {}: {}", input_path, e))?;

    // Deserialize from binary
    let mut stream = ByteArrayStream::new();
    stream.write_bytes(&data)?;
    stream.set_position(0);
    let world = BinaryTranslator::World_from_binary(&mut stream)?;

    // Reserialize to binary
    let mut out_stream = ByteArrayStream::new();
    BinaryTranslator::World_to_binary(&mut out_stream, &world)?;
    let bytes = out_stream.into_vec();

    // Write output file
    fs::write(output_path, &bytes)
        .map_err(|e| format!("Failed to write {}: {}", output_path, e))?;

    eprintln!("Successfully copied {} -> {}", input_path, output_path);
    Ok(())
}

fn main() {
    let args: Vec<String> = env::args().collect();
    let prog_name = &args[0];

    if args.len() != 3 {
        display_info(prog_name);
        process::exit(1);
    }

    let input_path = &args[1];
    let output_path = &args[2];

    if let Err(e) = binary_to_binary(input_path, output_path) {
        eprintln!("Error: {}", e);
        process::exit(1);
    }
}
