import psycopg2
import uuid_utils as uuid
import datetime

adminid = "00000000-0000-0000-0000-000000000000"

conn = psycopg2.connect(
    database = "maw_media",
    user = "postgres",
    host= 'localhost',
    port = 6543,
    cursor_factory = psycopg2.extras.RealDictCursor
)
cur = conn.cursor()

def loadData(query):
    loadCur = conn.cursor()
    loadCur.execute(query)
    records = loadCur.fetchall()
    loadCur.close()
    return records

def loadUsers():
    return loadData("SELECT id, username, first_name, last_name, email FROM maw.user")

def loadRoles():
    return loadData("SELECT id, name, description FROM maw.role")

def writeUsers(userRecords):
    f = open("/output/media.user.sql", "w")

    now = datetime.timezone.utc.now()
    sql = f"INSERT INTO media.user (id, created, modified, name, email, email_verified) VALUES ('{adminid}', '{now}', '{now}', 'admin', 'webmaster@mikeandwan.us', true)"

    f.write(str(sql, 'utf-8'))

    for row in userRecords:
        values = (uuid.uuid7().hex, now, now, f"{row["first_name"]} {row["last_name"]}", row["email"], False)
        sql = cur.mogrify("INSERT INTO media.user (id, created, modified, name, email, email_verified) VALUES (%s, %s, %s, %s, %s, %s)", values)
        f.write(str(sql, 'utf-8'))
        f.write(";\n")

    f.close()

users = loadUsers()
roles = loadRoles()

conn.close()

writeUsers(users)
