import os
import time
import json
from datetime import datetime, timedelta

# import socket
# from collections import deque
# from threading import Thread

# with open('D:\\smartd\\script\\mh02model\\files\\configs\\smartd.json', 'r') as s:
#     deviceconfig = json.load(s)

# DEVICE_ID = deviceconfig["DEVICE_ID"]
# SERVERIP = deviceconfig["SERVER_UPLOAD_IP"]
# SERVERIP = "10.2.187.242"
# SERVERPORT = "9008"
# SERVERPORT = deviceconfig["SERVER_UPLOAD_PORT"]
RECORDING_FOLDER = "D:\\pama\\ftp\\recording"
UPLOADED_FOLDER = "D:\\smartd\\script\\mh02evidenceuploader\\recording_uploaded"
WAITING_FOLDER = "D:\\smartd\\script\\mh02evidenceuploader\\recording_waiting"
TEMP_WAITING_FOLDER = "D:\\smartd\\script\\mh02evidenceuploader\\recording_waiting_temp"
RESERVED_FOLDER = "D:\\smartd\\script\\mh02evidenceuploader\\recording_reserved"
INVALID_FOLDER = "D:\\smartd\\script\\mh02evidenceuploader\\recording_invalid"
 
# SERVER_URL = f'http://{SERVERIP}:{SERVERPORT}/files'
# MAX_ITERATIONS = 10
RESET_FLAG = "evidencepackagerstop.id"

# p_cycle_counter = 0
# p_cycle_counter_max = 10
# p_succes_rate_min=20
# p_succes_rate_counter=0

# maxflagiddle = 30 # 30 seconds
# lastflagcheck = time.time()
# udp_port = 7878
# udp_data = deque(maxlen=1)
# currflag = deque(maxlen=1)
# g_isfirststart = True

def getReserved():
    global RESERVED_FOLDER

    # i_reserved_dir = os.listdir(RESERVED_FOLDER)
    i_reserved_files = []
    # _datajson = []

    # for item in i_reserved_dir:
    #     if os.path.isfile(RESERVED_FOLDER + "\\" + item) and item.split(".")[1] == "txt":
    #         if (len(item.split("_")) > 2):
    #             i_reserved_files.append(item.split(".")[0])

    #     elif os.path.isfile(RESERVED_FOLDER + "\\" + item) and item.split(".")[1] == "json":
    #         try:
    #             with open(RESERVED_FOLDER + "\\" + item, 'rb') as j:
    #                 _data = json.load(j)
    #                 for i in _data:
    #                     _datajson.append(i["filename"])
    #             os.remove(RESERVED_FOLDER + "\\" + item)
    #         except:
    #             os.remove(RESERVED_FOLDER + "\\" + item)
    #             pass

    # for item in _datajson:
    #     _filejson = os.path.join(RESERVED_FOLDER, f"{item}.txt")

    #     if os.path.exists(_filejson):
    #         continue

    #     try:
    #         with open(_filejson, 'w') as file:
    #             file.write(str(item))
    #     except Exception as e:
    #         print("[ERROR] Writing to Reserved file:", e)

    return i_reserved_files
   
def getAlerts(dirpath, filetype):
    i_dirs = os.listdir(dirpath)
    i_valid_dirs = []
    for item in i_dirs:
        if filetype == "DIR":
            if os.path.isdir(dirpath + "\\" + item):
                if (len(item.split("_")) > 2) and not "TESTING" in item:
                    i_valid_dirs.append(item)
        elif filetype == "FILE":
            # If filetype is FILE, remove file extension
            if os.path.isfile(dirpath + "\\" + item):
                if (len(item.split("_")) > 2) and not "TESTING" in item:
                    i_valid_dirs.append(item.split(".")[0])

    return i_valid_dirs


def checkIf0KPH(item):
    _filejson = os.path.join(RECORDING_FOLDER, item, f"{item}.json")
    _filetxt = os.path.join(RECORDING_FOLDER, item, f"{item}.txt")

    if os.path.exists(_filejson):
        print(f"File {_filejson} found. Checking 0 KPH with JSON.")
        try:
            with open(_filejson, 'r') as j:
                _data = json.load(j)
                return _data.get("speed") == "0"
        except Exception as e:
            print(f"[ERROR] Failed to check 0 KPH from JSON due to: {e}. Switching to TXT file check.")

    if not os.path.exists(_filetxt):
        print(f"File {_filetxt} not found.")
        return False

    try:
        print(f"Checking 0 KPH with TXT: {_filetxt}")
        _speed = '0'
        with open(_filetxt, 'r') as file:
            for line in file:
                fields = line.strip().split(',')
                if fields[3].strip() == 'ON':
                    _speed_line = fields[7].strip().strip("'")
                    if _speed_line > _speed:
                        _speed = _speed_line
                elif fields[3].strip() == 'POST':
                    break

        return _speed == '0'
    
    except Exception as e:
        print(f"[ERROR] Exception during TXT file check: {e}")
        return False



def compressVideo(item):
    i_result = False

    i_source_file = RECORDING_FOLDER + "\\" + item + "\\" + item + ".mp4"
    i_source_file_backup = RECORDING_FOLDER + "\\" + item + "\\" + item + "_BACKUP.mp4"
    i_temp_compressed_file = RECORDING_FOLDER + "\\" + item + "\\" + item + "_OUT.mp4"
    success_marker_file = RECORDING_FOLDER + "\\" + item + "\\" + item + "_COMPRESSED_SUCCESS.txt" 

    # Check if the compression success marker file exists, if so skip compression
    if os.path.exists(success_marker_file):
        print(f"[INFO] Compression already completed for {item}. Skipping...")
        return True
    
    # Remove temp file if exists
    if os.path.exists(i_temp_compressed_file):
        os.remove(i_temp_compressed_file)
        time.sleep(0.5)
        print(f"[INFO] Removed existing output file: {i_temp_compressed_file}")

    # If video doesn't exist, return false
    if not os.path.exists(i_source_file):
        if not os.path.exists(i_source_file_backup):
            i_result = False
        else:
            os.rename(i_source_file_backup, i_source_file)
        
    
    try:
        ffmpeg_command = f"ffmpeg -y -i {i_source_file} -vcodec libx264 -preset veryslow -crf 30 -b:v 300k -acodec aac -b:a 64k {i_temp_compressed_file}"
        i_compress_result = os.system(ffmpeg_command)

        time.sleep(0.5)

        if i_compress_result == 0:
            # Check if backup file exists, if so remove it
            if os.path.exists(i_source_file_backup):
                os.remove(i_source_file_backup)
                print(f"[INFO] Removed existing backup file: {i_source_file_backup}")

            os.rename(i_source_file, i_source_file_backup)
            os.rename(i_temp_compressed_file, i_source_file)
            time.sleep(0.5)  
            os.remove(i_source_file_backup)

            # Create a success marker file to indicate that compression is done
            with open(success_marker_file, 'w'):  # Create an empty file to mark success
                pass

            i_result = True
            print("[INFO] MP4 compression successful:", i_source_file)
        else:
            print("[ERROR] MP4 compression failed:", i_source_file)
            i_result = False

    except Exception as e:
            print("[ERROR] Exception during video processing:", e)
            i_result = False

    return i_result

def checkTimeLimit(_item):
    _now = datetime.now()
    _minus_one = _now + timedelta(minutes = -2)
    _minus_one_str = _minus_one.strftime("%Y%m%d%H%M%S")

    _item_arr = _item.split("_")
    _item_time_str = str(_item_arr[1]) + str(_item_arr[2])

    return (_item_time_str < _minus_one_str)


def PackagerEvidences():
    # global p_succes_rate_counter,p_cycle_counter

    p_recordings = []
    p_recordings_uploaded = []
    p_recordings_waiting = []
    p_newalerts = []
    # p_waiting_tobe_uploaded = []

    # p_invalid_files = []

    # Process #1
    # 1.a Get evidences from recording folder
    p_recordings = getAlerts(RECORDING_FOLDER,"DIR")
    print("Recording:", p_recordings)

    # 1.b Get uploaded evidences
    p_recordings_uploaded = getAlerts(UPLOADED_FOLDER,"FILE")
    print("Uploaded:", p_recordings_uploaded)

    # 1.c Get waiting evidences
    p_recordings_waiting = getAlerts(WAITING_FOLDER,"FILE")
    print("Waiting:", p_recordings_waiting)

    # Process #2
    # Consolidate Recording, Uploaded and Waiting
    # 2.a Consolidate Uploaded vs Recording
    for item in p_recordings_uploaded:
        # If item in uploaded still exists in recording => remove from recording
        if item in p_recordings:
            p_recordings.remove(item)
        else:
            # If item in uploaded doesn't exist anymore in recording => remove physical zip file in uploaded folder
            # Housekeeping for uploaded folder
            try:
                os.remove(UPLOADED_FOLDER + "\\"+ item + ".zip")
            except Exception as e:
                print("Failed on removing file:",UPLOADED_FOLDER + "\\"+ item + ".zip", e)

    # 2.b Consolidate Waiting vs Recording
    for item in p_recordings_waiting:
        # If item in waiting still exists in recording => remove from recording
        if item in p_recordings:
            p_recordings.remove(item)
        else:
            # If item in waiting doesn't exist anymore in recording => remove physical zip file in waiting folder
            # Housekeeping for uploaded folder
            try:
                # os.removedirs(WAITING_FOLDER + "\\"+ item + ".zip")
                # _item = WAITING_FOLDER + "\\"+ item + ".zip"
                # _remove_command = f"del {_item} /f"
                # os.system(_remove_command)
                os.remove(WAITING_FOLDER + "\\"+ item + ".zip")
            except Exception as e:
                print("Failed on removing file:",WAITING_FOLDER + "\\"+ item + ".zip", e)

    print("Only in Recording:", p_recordings)

    # Process #3 
    # Only new alert(s) left in recording array
    p_newalerts = p_recordings.copy()
    # print("New Recordings:", p_newalerts)
    
    # Check if any reserved alerts need to prioritize

    i_reserved_alerts = getReserved()
    if len(i_reserved_alerts) > 0:
        print("Found Reserved Alert:", i_reserved_alerts)
        p_newalerts = i_reserved_alerts + p_newalerts

    for item in p_newalerts:
        
        # 3.a If alert is fewer than 1 minutes, skip
        if not checkTimeLimit(item):
            print("Skip item", item, " because fewer than time limit.")
            continue

        # Check if folder does not exist, skip
        if not os.path.exists(RECORDING_FOLDER + "\\" + item):
            print("Skip item", item, " because folder does not exist in Recording.")
            continue

        # Check if item exist in waiting, skip
        if os.path.exists(WAITING_FOLDER + "\\" + item + ".zip"):
            print("Skip item", item, " because folder exist in Waiting.")
            continue

        # Check if item exist in waiting, skip
        if os.path.exists(UPLOADED_FOLDER + "\\" + item + ".zip"):
            print("Skip item", item, " because folder exist in Uploaded.")
            continue

        time.sleep(0.5)

        _zippingfile = False
        _zipwaiting = f"{WAITING_FOLDER}\\{item}.zip"
        _tempzipwaiting = f"{TEMP_WAITING_FOLDER}\\{item}.zip"
        _zipuploaded = f"{UPLOADED_FOLDER}\\{item}.zip"
        _zipsourcerec = f"{RECORDING_FOLDER}\\{item}\\*"
        _zipinvalidrec = f"{INVALID_FOLDER}\\{item}.zip"
        
        
        # 3.b If item is 0 kph alert, zip and move to uploaded folder
        i_is0kph = checkIf0KPH(item)
        if i_is0kph:
            # Gunakan Start-Process -Wait agar proses ZIP benar-benar selesai sebelum lanjut
            _command = f'powershell -Command "Start-Process -NoNewWindow -Wait powershell -ArgumentList \'Compress-Archive -Path \\"{_zipsourcerec}\\" -DestinationPath \\"{_zipuploaded}\\" -Force\'"'
            exec_res = os.system(_command)
            _zippingfile = True
            continue

        # 3.c If item is not 0 kph, validate video file by compressing the file
        i_compress_status = compressVideo(item)

        # 3.d If video file is valid, zip folder and send it into waiting folder
        if i_compress_status:
            print("Zipping file:", item)
            _command = f'powershell -Command "Start-Process -NoNewWindow -Wait powershell -ArgumentList \'Compress-Archive -Path \\"{_zipsourcerec}\\" -DestinationPath \\"{_tempzipwaiting}\\" -Force\'"'
            exec_res = os.system(_command)
            time.sleep(0.5)

            try:
                if os.path.exists(_tempzipwaiting):
                    copy_command = f'powershell -Command "Start-Process -NoNewWindow -Wait powershell -ArgumentList \'Copy-Item -Path \\"{_tempzipwaiting}\\" -Destination \\"{_zipwaiting}\\" -Force\'"'
                    copy_res = os.system(copy_command)

                    if copy_res == 0:
                        time.sleep(0.5)
                        os.remove(_tempzipwaiting)
                        _zippingfile = True
                        print(f"[INFO] Zip file moved to: {_zipwaiting}")
                    else:
                        print("[ERROR] Failed to copy zip file:", _tempzipwaiting)
                        _zippingfile = False
                else:
                    print("[ERROR] Temp zip file not found:", _tempzipwaiting)
                    _zippingfile = False
            except Exception as e:
                print("[ERROR] Exception during zipping:", e)
                _zippingfile = False
            
        else:
            # Add invalid item into invalid file list
            # p_invalid_files.append(item)
            # ZIp invalid item and move to waiting folder

            print("Zipping invalid file:", item)
            _command = f'powershell -Command "Start-Process -NoNewWindow -Wait powershell -ArgumentList \'Compress-Archive -Path \\"{_zipsourcerec}\\" -DestinationPath \\"{_zipinvalidrec}\\" -Force\'"'
            exec_res = os.system(_command)
            _zippingfile = True
            
         # 3.e If exist reserved, remove

        # if _zippingfile:
        #     if os.path.exists(RESERVED_FOLDER + "\\" + item + ".txt"):
        #         os.remove(RESERVED_FOLDER + "\\" + item + ".txt")
        #         print(f"Remove item {item} from {RESERVED_FOLDER}")
        
        print("======= Alert:", item, " Zip Result:",_zippingfile, " Compress result: ", i_compress_status)

    # # Process #4
    # # Upload zip files to server
    # # 4.a Collect waiting zip files
    # p_waiting_tobe_uploaded = getAlerts(WAITING_FOLDER,"FILE")
    # print("Waiting tobe uploaded:", p_waiting_tobe_uploaded)

    # # 4.b Sort waiting zip files descending
    # p_waiting_tobe_uploaded.sort(key=lambda x: (x.split("_")[1] + x.split("_")[2]),reverse=True)
    # # filtered_list = [item for item in p_waiting_tobe_uploaded if 'CLOSEDEYES' in item or 'YAWNING' in item]
    # # other_list = [item for item in p_waiting_tobe_uploaded if 'CLOSEDEYES' not in item and 'YAWNING' not in item]

    # # filtered_list.sort(key=lambda x: (x.split("_")[1], x.split("_")[2]), reverse=True)
    # # other_list.sort(key=lambda x: (x.split("_")[1], x.split("_")[2]), reverse=True)

    # # p_waiting_tobe_uploaded = filtered_list + other_list
    # print("Waiting tobe uploaded (sorted desc):", p_waiting_tobe_uploaded)

    # # 4.c Upload zip files
    # for item in p_waiting_tobe_uploaded[0:MAX_ITERATIONS]:
    #     try:
    #         with open(WAITING_FOLDER + "\\" + item + ".zip", 'rb') as zipf:
                
    #             if item in p_invalid_files:
    #                 _file_status = "INVALID"
    #             else:
    #                 _file_status = "VALID"

    #             zipfile = {'file': zipf}
    #             resp = requests.post(url=SERVER_URL + "?deviceid=" + DEVICE_ID + "&filestatus=" + _file_status, files=zipfile, timeout=30)

    #         # zipf = open(WAITING_FOLDER + "\\" + item + ".zip", 'rb')
    #         # zipfile = {'file': zipf}
    #         # zipf.close()

    #         if resp.text == "OK":
    #             # 4.d Move succeeded zip to uploaded folder
    #             print("Upload evidence:", item, " is success.")
    #             shutil.move(WAITING_FOLDER + "\\" + item + ".zip", UPLOADED_FOLDER + "\\" + item + ".zip")
    #             p_succes_rate_counter = p_succes_rate_counter+1

    #         elif resp.text == "CORRUPT":
    #             os.remove(WAITING_FOLDER + "\\"+ item + ".zip")

    #         else:
    #             print("Uploading evidence:", item, " is failed.")
    #         time.sleep(2)

    #     except Exception as ex:
    #         print("[ERROR] Exception Upload:", ex)

# def run_udp_server():
#     global mh02config, done
#     global udp, udp_port, udp_data

#     print("UDP Starting")

#     try:
#         ip = "0.0.0.0"
#         udp =  socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
#         udp.bind((ip, udp_port))
#     except Exception as e:
#         print(datetime.now(),"[ERROR] Running UDP Server :", e)


#     while True:
#         data, address = udp.recvfrom(1024)
#         udp_data.append(data.decode('utf-8'))

#         if os.path.exists(RESET_FLAG):
#             print("UDP is stopped because service is stopped.")
#             break

#         time.sleep(1/30)

#     return True

# def handle_flags():
#     global udp
#     global currflag, lastflagcheck, udp_data, g_isfirststart, p_cycle_counter, p_succes_rate_counter

#     if g_isfirststart:
#         g_isfirststart = False
#         currflag = 1

#     if len(udp_data) > 0:
#         try:
#             _udp_data = udp_data.pop()
#             currflag = int(_udp_data)
#             print(datetime.now(), f"[INFO] Receive response from server: {currflag}")
#         except:
#             print(datetime.now(), f"[ERROR] Receive response from server is not valid number:", _udp_data)
#             pass
    
#     if currflag == 1:
#         _logres = uploadEvidences()
#         print(datetime.now(), "Running Evidence Uploader...")

#         p_cycle_counter = p_cycle_counter + 1
#         print(datetime.now(), "Cycle Counter Device", p_cycle_counter)
#         if p_cycle_counter>p_cycle_counter_max:
#             p_cycle_counter=0
#             print(datetime.now(), "Cycle Counter Reset", p_cycle_counter)
#             if p_succes_rate_counter<p_succes_rate_min:
#                 currflag=0            
#                 p_succes_rate_counter=0
#                 print(datetime.now(),"Success Rate Reset", p_succes_rate_counter)

#         print(datetime.now(),"Current Flag Uploader Device", currflag)

#         lastflagcheck = time.time()

#     elif currflag == 0:
#         print(datetime.now(), "Counter to reset flag:", maxflagiddle - int((time.time() - lastflagcheck)))
       
#         if int((time.time() - lastflagcheck)) >= maxflagiddle:
#             lastflagcheck = time.time()  
#             currflag = 1  
#             print(datetime.now(), "Stop Evidence Uploader...")


#     elif currflag == 9:
 
#         # udp = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

#         _response = {
#             "deviceid" : DEVICE_ID,
#             "currflag" : currflag
#         }

#         response = json.dumps(_response)
        
#         udp.sendto(response.encode('utf-8'), (SERVERIP, udp_port))
#         print(datetime.now(), f"Sent response to server: {response}")
#         currflag = 1 
#         print(datetime.now(), "Reset Default flag to :", currflag)
    
#     else:
#         print(datetime.now(),"[ERROR] Unknown Flag", currflag, ". Flag is reset to 1.")
#         currflag = 1


# Main Process
if not os.path.exists(RESERVED_FOLDER):
    os.mkdir(RESERVED_FOLDER)

if not os.path.exists(TEMP_WAITING_FOLDER):
    os.mkdir(TEMP_WAITING_FOLDER)

if not os.path.exists(INVALID_FOLDER):
    os.mkdir(INVALID_FOLDER)

# udp =  socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
# Thread(target=run_udp_server, args=()).start()  

while(True):

    # print("::::::::::::INFORMATION::::::::::::")
    print("*******************************************", datetime.now())
    # print(f"Running ON {DEVICE_ID}, {SERVERIP}, {SERVERPORT}")

    PackagerEvidences()

    time.sleep(1)

    if os.path.exists(RESET_FLAG):
        os.remove(RESET_FLAG)
        print("Service is stopping.....")
        time.sleep(2)
        print("Service is stopped.")
        break
