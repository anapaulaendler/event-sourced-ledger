CREATE OR REPLACE FUNCTION notify_events_appended()
RETURNS trigger AS $$
BEGIN
    PERFORM pg_notify('events_appended', NEW.sequence::text);
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER events_appended_notify
AFTER INSERT ON events
FOR EACH ROW EXECUTE FUNCTION notify_events_appended();
