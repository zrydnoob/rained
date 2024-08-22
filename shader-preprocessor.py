# GLSL shader preprocessor.
# Performs offline shader validation as well as an implementation
# of the #include directive, allowing shaders to include other files
# from the filesystem.
# TODO: rewrite this as a c# project in the src directory ('src/ShaderPreprocessor')

import os
import io
import sys
import re
import subprocess
from os import path

# get files in glshaders list that has the extension .vert.glsl or .frag.glsl
# these will be recognized as source files that need to be processed and validated
sources = []
for f in os.listdir('glshaders'):
    abs_path = os.path.join('glshaders', f)

    if len(abs_path) >= 10:
        ext = abs_path[-10:]
        if ext == '.vert.glsl' or ext == ".frag.glsl":
            sources.append(f)

class ProcessData:
    def __init__(self):
        self.processed = []

class ValidationException(Exception):
    def __init__(self, source, line, data, message):
        super().__init__(f"{source}:{line}: '{data}' : {message}")

class CompilationException(Exception):
    def __init__(self, errors):
        super().__init__(f"{(len(errors))} compilation errors.  No code generated.")
        self.errors = errors

def process_file(in_file_path, out_file, proc_data):
    in_abs_path = os.path.abspath(os.path.join('glshaders', in_file_path))
    in_file_path = os.path.relpath(in_abs_path, start=os.path.join(os.curdir, 'glshaders'))
    line_num = 0

    with open(in_abs_path, 'r') as in_file:
        # reset line number and file id
        file_id = len(proc_data.processed)
        if in_abs_path in proc_data.processed:
            file_id = proc_data.processed.index(in_abs_path)
        else:
            proc_data.processed.append(in_abs_path)
        
        if file_id != 0:
            out_file.write("#line 1 " + str(file_id) + "\n")

        for line in in_file:
            line_num += 1
            line = line.rstrip()

            # #include directive
            stripped_line = line.lstrip()
            if stripped_line[0:9] == '#include ':
                include_path = stripped_line[9:].lstrip()

                first_char = include_path[0]
                last_char = include_path[-1]

                # check containing symbols
                if first_char != '"' and first_char != '<':
                    raise CompilationException([
                        ValidationException(in_file_path, line_num, first_char, "expected '<' or '\"'"),
                        ValidationException(in_file_path, line_num+1, "", "compilation terminated")
                    ])
                
                if first_char == '"' and last_char != '"':
                    raise CompilationException([
                        ValidationException(in_file_path, line_num, "", "expected '\"', got EOL"),
                        ValidationException(in_file_path, line_num+1, "", "compilation terminated")
                    ])
                
                if first_char == '<' and last_char != '>':
                    raise CompilationException([
                        ValidationException(in_file_path, line_num, "", "expected '>', got EOL"),
                        ValidationException(in_file_path, line_num+1, "", "compilation terminated")
                    ])

                # include file into source
                process_file(include_path[1:-1], out_file, proc_data)
                line_num += 1
                out_file.write(f"#line {line_num} {file_id}\n")

            else:
                out_file.write(line + '\n')

success = True
os.makedirs('glshaders/build', exist_ok=True)

for src_name in sources:
    # generate preprocessor output
    proc_data = ProcessData()
    out_file_path = 'glshaders/build/' + src_name
    with open(out_file_path, 'w') as out_file:
        try:
            process_file(src_name, out_file, proc_data)
        except CompilationException as e:
            for ve in e.errors:
                print("ERROR: " + str(ve))
            print("ERROR: " + str(e))
            success = False
    
    # validate code
    glslang = subprocess.Popen(['glslang', out_file_path], stdout=subprocess.PIPE)
    for line in io.TextIOWrapper(glslang.stdout, encoding='utf-8'):
        line = line.strip()

        # only print errors
        if line[0:7] == 'ERROR: ':
            # replace the file index with the file name the code has
            # associated with it, before printing the error
            re_res = re.search(r'(\d+)\:\d+', line[7:])

            if re_res == None:
                print(line)
            
            else:
                file_id = int(re_res.group(1))
                file_name = os.path.relpath(proc_data.processed[file_id], start=os.path.join(os.curdir, 'glshaders'))
                line = f"ERROR: {file_name}" + line[(7 + len(re_res.group(1))):]
                print(line)

                # print include chain
                while file_id > 0:
                    file_id -= 1
                    includer_name = os.path.relpath(proc_data.processed[file_id], start=os.path.join(os.curdir, 'glshaders'))
                    print(f"       (included from {includer_name})")

            success = False

# exit with an error code if there were errors
if not success:
    sys.exit(1)